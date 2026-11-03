using System.Data;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Acity.Infrastructure.Persistence;

/// <summary><c>usp_CambiarEstadoCarga</c> con EF Core (sin Dapper), fuera de transacciones y con <c>@EstadoEsperado</c> (V003).</summary>
public sealed class ProcedimientosCarga(
    AppDbContext context,
    [FromKeyedServices(PipelinesResiliencia.Sql)] ResiliencePipeline pipeline) : IProcedimientosCarga
{
    public async Task<bool> CambiarEstadoAsync(long cargaId, string nuevoEstado, string? estadoEsperado, CancellationToken cancellationToken)
        => await pipeline.EjecutarAsync(PipelinesResiliencia.Sql, async ct =>
        {
            var cambiado = new SqlParameter("@Cambiado", SqlDbType.Bit) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_CambiarEstadoCarga @CargaId = @CargaId, @NuevoEstado = @NuevoEstado, @Cambiado = @Cambiado OUTPUT, @EstadoEsperado = @EstadoEsperado",
                [
                    new SqlParameter("@CargaId", SqlDbType.BigInt) { Value = cargaId },
                    new SqlParameter("@NuevoEstado", SqlDbType.VarChar, 20) { Value = nuevoEstado },
                    cambiado,
                    new SqlParameter("@EstadoEsperado", SqlDbType.VarChar, 20) { Value = (object?)estadoEsperado ?? DBNull.Value }
                ],
                ct);
            return cambiado.Value is true;
        }, cancellationToken);
}
