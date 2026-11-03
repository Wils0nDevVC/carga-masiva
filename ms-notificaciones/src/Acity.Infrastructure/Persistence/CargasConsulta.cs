using Acity.Application.Interfaces.Services;
using Acity.Application.Notificaciones.Correo;
using Acity.Domain.Entities;
using Acity.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Acity.Infrastructure.Persistence;

/// <summary>Lectura del estado de la carga (pipeline SQL) y registro de ErrorNotificacion (sin reintentos).</summary>
public sealed class CargasConsulta(
    AppDbContext context,
    [FromKeyedServices(PipelinesResiliencia.Sql)] ResiliencePipeline pipeline) : ICargasConsulta
{
    private const int LongitudMotivo = 500;

    public async Task<string?> ObtenerEstadoAsync(long idCarga, CancellationToken cancellationToken)
        => await pipeline.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
            await context.Set<CargaArchivo>().AsNoTracking()
                .Where(c => c.Id == idCarga)
                .Select(c => c.Estado)
                .FirstOrDefaultAsync(ct), cancellationToken);

    /// <summary>Máximo de incidencias detalladas en el correo (Feature 016, Q1).</summary>
    private const int MaximoIncidenciasDetalle = 20;

    public async Task<ResumenCarga> ObtenerResumenAsync(long idCarga, CancellationToken cancellationToken)
        => await pipeline.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
        {
            var nombre = await context.Set<CargaArchivo>().AsNoTracking()
                .Where(c => c.Id == idCarga)
                .Select(c => c.NombreArchivo)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
            var periodos = await context.Set<CargaPeriodo>().AsNoTracking()
                .Where(p => p.CargaId == idCarga)
                .OrderBy(p => p.Periodo)
                .Select(p => new PeriodoResumen(p.Periodo, p.Resultado))
                .ToListAsync(ct);
            var incidencias = context.Set<Incidencia>().AsNoTracking().Where(i => i.CargaId == idCarga);
            var porTipo = await incidencias
                .GroupBy(i => i.Tipo)
                .Select(g => new TipoRecuento(g.Key, g.Count()))
                .ToListAsync(ct);
            var primeras = await incidencias
                .OrderBy(i => i.Id)
                .Take(MaximoIncidenciasDetalle)
                .Select(i => new IncidenciaResumen(i.Tipo, i.NumeroFila, i.CodigoProducto, i.PeriodoTexto, i.Motivo))
                .ToListAsync(ct);
            return new ResumenCarga(
                nombre,
                periodos,
                porTipo.Sum(t => t.Cantidad),
                porTipo.OrderByDescending(t => t.Cantidad).ThenBy(t => t.Tipo).ToList(),
                primeras);
        }, cancellationToken);

    public async Task RegistrarErrorNotificacionAsync(long idCarga, string motivo, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        context.Set<Incidencia>().Add(new Incidencia
        {
            CargaId = idCarga,
            CargaPeriodoId = null,
            Tipo = "ErrorNotificacion",
            NumeroFila = null,
            CodigoProducto = null,
            PeriodoTexto = null,
            Motivo = motivo.Length > LongitudMotivo ? motivo[..LongitudMotivo] : motivo,
            FechaRegistro = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }
}
