using System.Data;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Acity.Infrastructure.Persistence;

/// <summary>
/// Invoca los procedimientos de la carga con EF Core (sin Dapper), fuera de transacciones de la aplicación y con
/// parámetros con nombre (contrato de F001 y V003). Ambos son idempotentes: reintentarlos no duplica efectos.
/// </summary>
public sealed class ProcedimientosCarga(
    AppDbContext context,
    [FromKeyedServices(PipelinesResiliencia.Sql)] ResiliencePipeline pipeline) : IProcedimientosCarga
{
    public async Task<string> ReservarPeriodoAsync(long cargaId, string periodo, CancellationToken cancellationToken)
        => await pipeline.ExecuteAsync(async ct =>
        {
            var resultado = new SqlParameter("@Resultado", SqlDbType.VarChar, 20) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_ReservarPeriodo @CargaId = @CargaId, @Periodo = @Periodo, @Resultado = @Resultado OUTPUT",
                [
                    new SqlParameter("@CargaId", SqlDbType.BigInt) { Value = cargaId },
                    new SqlParameter("@Periodo", SqlDbType.Char, 7) { Value = periodo },
                    resultado
                ],
                ct);
            return (string)resultado.Value;
        }, cancellationToken);

    public async Task<bool> CambiarEstadoAsync(long cargaId, string nuevoEstado, string? periodoFallido, string? estadoEsperado, CancellationToken cancellationToken)
        => await pipeline.ExecuteAsync(async ct =>
        {
            var cambiado = new SqlParameter("@Cambiado", SqlDbType.Bit) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_CambiarEstadoCarga @CargaId = @CargaId, @NuevoEstado = @NuevoEstado, @PeriodoFallido = @PeriodoFallido, @Cambiado = @Cambiado OUTPUT, @EstadoEsperado = @EstadoEsperado",
                [
                    new SqlParameter("@CargaId", SqlDbType.BigInt) { Value = cargaId },
                    new SqlParameter("@NuevoEstado", SqlDbType.VarChar, 20) { Value = nuevoEstado },
                    new SqlParameter("@PeriodoFallido", SqlDbType.Char, 7) { Value = (object?)periodoFallido ?? DBNull.Value },
                    cambiado,
                    new SqlParameter("@EstadoEsperado", SqlDbType.VarChar, 20) { Value = (object?)estadoEsperado ?? DBNull.Value }
                ],
                ct);
            return cambiado.Value is true;
        }, cancellationToken);
}
