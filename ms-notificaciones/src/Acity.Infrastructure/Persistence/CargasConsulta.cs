using Acity.Application.Interfaces.Services;
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
