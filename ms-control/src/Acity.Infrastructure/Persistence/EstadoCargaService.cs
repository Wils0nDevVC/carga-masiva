using System.Data;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Acity.Infrastructure.Persistence;

/// <summary>
/// Invoca <c>usp_CambiarEstadoCarga</c> con EF Core (sin Dapper), fuera de cualquier transacción abierta, con el
/// parámetro opcional <c>@EstadoEsperado</c> de V003: la comprobación del origen y el cambio son atómicos.
/// </summary>
public sealed class EstadoCargaService(
    AppDbContext context,
    [FromKeyedServices(PipelinesResiliencia.Procedimiento)] ResiliencePipeline pipeline) : IEstadoCargaService
{
    public async Task<bool> PasarAFallidoDesdePendienteAsync(long cargaId, CancellationToken cancellationToken)
        => await pipeline.ExecuteAsync(async ct =>
        {
            var cambiado = new SqlParameter("@Cambiado", SqlDbType.Bit) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_CambiarEstadoCarga @CargaId = @CargaId, @NuevoEstado = @NuevoEstado, @Cambiado = @Cambiado OUTPUT, @EstadoEsperado = @EstadoEsperado",
                [
                    new SqlParameter("@CargaId", SqlDbType.BigInt) { Value = cargaId },
                    new SqlParameter("@NuevoEstado", SqlDbType.VarChar, 20) { Value = "Fallido" },
                    cambiado,
                    new SqlParameter("@EstadoEsperado", SqlDbType.VarChar, 20) { Value = "Pendiente" }
                ],
                ct);
            return cambiado.Value is true;
        }, cancellationToken);
}
