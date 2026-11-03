using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Interfaces.Repositories;
using Acity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acity.Infrastructure.Persistence;

/// <summary>Consultas de solo lectura (AsNoTracking) con el filtro por propietario dentro de la consulta.</summary>
public sealed class CargasConsulta(AppDbContext context) : ICargasConsulta
{
    public async Task<IReadOnlyList<CargaResumenDto>> ListarAsync(int usuarioId, bool esAdministrador, CancellationToken cancellationToken)
    {
        var filas = await Visibles(usuarioId, esAdministrador)
            .Join(context.Set<Usuario>(), c => c.UsuarioId, u => u.Id, (c, u) => new { c, u.Email })
            .OrderByDescending(x => x.c.FechaRecepcion).ThenByDescending(x => x.c.Id)
            .Select(x => new { x.c.Id, x.c.NombreArchivo, x.Email, x.c.FechaRecepcion, x.c.Estado, x.c.FechaFin })
            .ToListAsync(cancellationToken);

        return filas.Select(x => new CargaResumenDto(x.Id, x.NombreArchivo, x.Email, Utc(x.FechaRecepcion), x.Estado, Utc(x.FechaFin))).ToList();
    }

    public async Task<CargaDetalleDto?> ObtenerDetalleAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken cancellationToken)
    {
        var carga = await Visibles(usuarioId, esAdministrador)
            .Where(c => c.Id == idCarga)
            .Join(context.Set<Usuario>(), c => c.UsuarioId, u => u.Id, (c, u) => new { c.Id, c.NombreArchivo, u.Email, c.FechaRecepcion, c.Estado, c.FechaFin })
            .FirstOrDefaultAsync(cancellationToken);
        if (carga is null)
        {
            return null;
        }

        var historial = await context.Set<CargaEstadoHistorial>().AsNoTracking()
            .Where(h => h.CargaId == idCarga)
            .OrderBy(h => h.FechaCambio).ThenBy(h => h.Id)
            .Select(h => new { h.Estado, h.FechaCambio })
            .ToListAsync(cancellationToken);
        var periodos = await context.Set<CargaPeriodo>().AsNoTracking()
            .Where(p => p.CargaId == idCarga)
            .OrderBy(p => p.FechaResultado).ThenBy(p => p.Id)
            .Select(p => new { p.Periodo, p.Resultado, p.FechaResultado })
            .ToListAsync(cancellationToken);
        var incidencias = await context.Set<Incidencia>().AsNoTracking()
            .Where(i => i.CargaId == idCarga)
            .OrderBy(i => i.Id)
            .Select(i => new { i.Tipo, i.NumeroFila, i.CodigoProducto, i.PeriodoTexto, i.Motivo, i.FechaRegistro })
            .ToListAsync(cancellationToken);

        return new CargaDetalleDto(
            carga.Id, carga.NombreArchivo, carga.Email, Utc(carga.FechaRecepcion), carga.Estado, Utc(carga.FechaFin),
            historial.Select(h => new EstadoHistorialDto(h.Estado, Utc(h.FechaCambio))).ToList(),
            periodos.Select(p => new ResultadoPeriodoDto(p.Periodo, p.Resultado, Utc(p.FechaResultado))).ToList(),
            incidencias.Select(i => new IncidenciaDto(i.Tipo, i.NumeroFila, i.CodigoProducto, i.PeriodoTexto, i.Motivo, Utc(i.FechaRegistro))).ToList());
    }

    public async Task<ArchivoCarga?> ObtenerArchivoAsync(long idCarga, int usuarioId, bool esAdministrador, CancellationToken cancellationToken) =>
        await Visibles(usuarioId, esAdministrador)
            .Where(c => c.Id == idCarga)
            .Select(c => new ArchivoCarga(c.NombreArchivo, c.RutaArchivo))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<CargaArchivo> Visibles(int usuarioId, bool esAdministrador) =>
        context.Set<CargaArchivo>().AsNoTracking().Where(c => esAdministrador || c.UsuarioId == usuarioId);

    // Las columnas DATETIME2 guardan UTC (F005, F006); se marca el tipo para que el JSON lleve "Z".
    private static DateTime Utc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? fecha) => fecha is null ? null : Utc(fecha.Value);
}
