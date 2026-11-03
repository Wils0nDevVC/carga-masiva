using Acity.Application.Interfaces.Repositories;
using Acity.Domain.Entities;
using Acity.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Acity.Infrastructure.Persistence;

/// <summary>
/// Persistencia del procesamiento con EF Core y LINQ (constitución §6). Lecturas y el incremento de intentos usan el
/// pipeline SQL; la transacción de un periodo usa el suyo y empieza cada intento comprobando si el periodo ya quedó
/// Completado (idempotencia ante un commit ambiguo). Las inserciones de incidencias no se reintentan.
/// </summary>
public sealed class RepositorioProcesamiento(
    AppDbContext context,
    [FromKeyedServices(PipelinesResiliencia.Sql)] ResiliencePipeline sql,
    [FromKeyedServices(PipelinesResiliencia.TransaccionGrupo)] ResiliencePipeline transaccionGrupo,
    ILogger<RepositorioProcesamiento> logger) : IRepositorioProcesamiento
{
    private const string IndiceCodigoProducto = "UX_Producto_CodigoProducto";
    private const int LoteCodigos = 1000;

    public async Task<CargaArchivo?> ObtenerCargaAsync(long cargaId, CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
            await context.Set<CargaArchivo>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == cargaId, ct), cancellationToken);

    public async Task<IReadOnlyList<CargaArchivo>> ObtenerCargasARecuperarAsync(CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
            (IReadOnlyList<CargaArchivo>)await context.Set<CargaArchivo>().AsNoTracking()
                .Where(c => c.Estado == "En proceso" || c.Estado == "Cargado")
                .OrderBy(c => c.Id)
                .ToListAsync(ct), cancellationToken);

    public async Task<int> IncrementarIntentosAsync(long cargaId, CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
        {
            // Una sola sentencia: incrementa y devuelve el valor nuevo.
            var valores = await context.Database
                .SqlQuery<int>($"UPDATE dbo.CargaArchivo SET IntentosProceso = IntentosProceso + 1 OUTPUT inserted.IntentosProceso AS Value WHERE Id = {cargaId}")
                .ToListAsync(ct);
            return valores.Single();
        }, cancellationToken);

    public async Task DescontarIntentoAsync(long cargaId, CancellationToken cancellationToken)
        => await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.CargaArchivo SET IntentosProceso = IntentosProceso - 1 WHERE Id = {cargaId} AND IntentosProceso > 0",
            cancellationToken);

    public async Task<IReadOnlyDictionary<string, (long Id, string Resultado)>> ObtenerPeriodosAsync(long cargaId, CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
        {
            var periodos = await context.Set<CargaPeriodo>().AsNoTracking()
                .Where(p => p.CargaId == cargaId)
                .Select(p => new { p.Periodo, p.Id, p.Resultado })
                .ToListAsync(ct);
            return (IReadOnlyDictionary<string, (long, string)>)periodos.ToDictionary(p => p.Periodo, p => (p.Id, p.Resultado));
        }, cancellationToken);

    public async Task<IReadOnlySet<string>> ObtenerCodigosDeOtrasCargasAsync(long cargaId, IReadOnlyCollection<string> codigos, CancellationToken cancellationToken)
    {
        var encontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lote in codigos.Chunk(LoteCodigos))
        {
            // La comparación la hace la base con la collation de la columna (autoridad, F001 R-07). Se devuelven los
            // códigos solicitados que tienen coincidencia, no los almacenados.
            var existentes = await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
                await (from p in context.Set<Producto>().AsNoTracking()
                       join cp in context.Set<CargaPeriodo>() on p.CargaPeriodoId equals cp.Id
                       where cp.CargaId != cargaId && lote.Contains(p.CodigoProducto)
                       select p.CodigoProducto).ToListAsync(ct), cancellationToken);

            var almacenados = new HashSet<string>(existentes, StringComparer.OrdinalIgnoreCase);
            encontrados.UnionWith(lote.Where(almacenados.Contains));
        }

        return encontrados;
    }

    public async Task<bool> ExisteIncidenciaSinPeriodoAsync(long cargaId, string tipo, CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
            await context.Set<Incidencia>().AsNoTracking()
                .AnyAsync(i => i.CargaId == cargaId && i.CargaPeriodoId == null && i.Tipo == tipo, ct), cancellationToken);

    public async Task RegistrarIncidenciasAsync(IReadOnlyCollection<Incidencia> incidencias, CancellationToken cancellationToken)
    {
        if (incidencias.Count == 0)
        {
            return;
        }

        context.ChangeTracker.Clear();
        await context.Set<Incidencia>().AddRangeAsync(incidencias, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    public async Task<ResultadoPersistenciaPeriodo> PersistirPeriodoAsync(
        long cargaPeriodoId,
        IReadOnlyList<Producto> productos,
        IReadOnlyList<Incidencia> incidencias,
        Func<Producto, Incidencia> existentePorColision,
        CancellationToken cancellationToken)
        => await transaccionGrupo.EjecutarAsync(PipelinesResiliencia.TransaccionGrupo, async ct =>
        {
            try
            {
                return await PersistirAsync(cargaPeriodoId, productos, incidencias, filaAFila: false, existentePorColision, ct);
            }
            catch (DbUpdateException ex) when (EsColisionDeCodigo(ex))
            {
                logger.LogWarning("Colisión de código de producto en el periodo {CargaPeriodoId}: se repite producto a producto", cargaPeriodoId);
                return await PersistirAsync(cargaPeriodoId, productos, incidencias, filaAFila: true, existentePorColision, ct);
            }
        }, cancellationToken);

    public async Task<(string Email, DateTime? FechaFin)> ObtenerDatosNotificacionAsync(long cargaId, CancellationToken cancellationToken)
        => await sql.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
        {
            var datos = await (from c in context.Set<CargaArchivo>().AsNoTracking()
                               join u in context.Set<Usuario>().AsNoTracking() on c.UsuarioId equals u.Id
                               where c.Id == cargaId
                               select new { u.Email, c.FechaFin }).SingleAsync(ct);
            return (datos.Email, datos.FechaFin);
        }, cancellationToken);

    private async Task<ResultadoPersistenciaPeriodo> PersistirAsync(
        long cargaPeriodoId,
        IReadOnlyList<Producto> productos,
        IReadOnlyList<Incidencia> incidencias,
        bool filaAFila,
        Func<Producto, Incidencia> existentePorColision,
        CancellationToken ct)
    {
        context.ChangeTracker.Clear();
        await using var transaccion = await context.Database.BeginTransactionAsync(ct);

        var periodo = await context.Set<CargaPeriodo>().SingleAsync(p => p.Id == cargaPeriodoId, ct);
        if (periodo.Resultado == "Completado")
        {
            context.ChangeTracker.Clear();
            return ResultadoPersistenciaPeriodo.YaCompletado;
        }

        var colisiones = 0;
        if (!filaAFila)
        {
            await context.Set<Producto>().AddRangeAsync(productos.Select(Copiar), ct);
            await context.Set<Incidencia>().AddRangeAsync(incidencias.Select(Copiar), ct);
        }
        else
        {
            // Un SaveChanges por producto dentro de la transacción: EF Core crea un punto de guardado y vuelve a él si
            // falla, de modo que un producto que colisiona no revierte los demás.
            foreach (var producto in productos)
            {
                var entidad = Copiar(producto);
                context.Set<Producto>().Add(entidad);
                try
                {
                    await context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (EsColisionDeCodigo(ex))
                {
                    context.Entry(entidad).State = EntityState.Detached;
                    context.Set<Incidencia>().Add(existentePorColision(producto));
                    colisiones++;
                }
            }

            await context.Set<Incidencia>().AddRangeAsync(incidencias.Select(Copiar), ct);
        }

        periodo.Resultado = "Completado";
        periodo.FechaResultado = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        context.ChangeTracker.Clear();

        return colisiones > 0 ? ResultadoPersistenciaPeriodo.CompletadoConColisiones : ResultadoPersistenciaPeriodo.Completado;
    }

    private static bool EsColisionDeCodigo(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 } sql
           && sql.Message.Contains(IndiceCodigoProducto, StringComparison.Ordinal);

    // Copias nuevas en cada intento: una entidad de un intento revertido no se reutiliza.
    private static Producto Copiar(Producto p) => new()
    {
        CodigoProducto = p.CodigoProducto,
        NombreProducto = p.NombreProducto,
        Precio = p.Precio,
        CargaPeriodoId = p.CargaPeriodoId
    };

    private static Incidencia Copiar(Incidencia i) => new()
    {
        CargaId = i.CargaId,
        CargaPeriodoId = i.CargaPeriodoId,
        Tipo = i.Tipo,
        NumeroFila = i.NumeroFila,
        CodigoProducto = i.CodigoProducto,
        PeriodoTexto = i.PeriodoTexto,
        Motivo = i.Motivo,
        FechaRegistro = i.FechaRegistro
    };
}
