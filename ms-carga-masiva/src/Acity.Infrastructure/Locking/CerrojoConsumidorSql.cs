using System.Data;
using Acity.Application.Interfaces.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Acity.Infrastructure.Locking;

/// <summary>
/// Cerrojo de aplicación de SQL Server de ámbito de sesión (contrato <c>cerrojo-consumidor.md</c>): exclusivo, sin
/// espera, sobre una conexión dedicada sin pool que vive mientras el consumidor tiene el cerrojo. Si el proceso muere,
/// SQL Server lo libera al cerrar la sesión.
/// </summary>
public sealed class CerrojoConsumidorSql(IConfiguration configuration, ILogger<CerrojoConsumidorSql> logger)
    : ICerrojoConsumidor, IAsyncDisposable
{
    public const string Recurso = "carga-masiva:consumidor-cargas";

    private SqlConnection? _conexion;

    public async Task<bool> IntentarObtenerAsync(CancellationToken cancellationToken)
    {
        await LiberarAsync();

        var cadena = new SqlConnectionStringBuilder(configuration.GetConnectionString("Default")) { Pooling = false };
        var conexion = new SqlConnection(cadena.ConnectionString);
        try
        {
            await conexion.OpenAsync(cancellationToken);
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "sys.sp_getapplock";
            comando.CommandType = CommandType.StoredProcedure;
            comando.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = Recurso });
            comando.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.VarChar, 32) { Value = "Exclusive" });
            comando.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.VarChar, 32) { Value = "Session" });
            comando.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int) { Value = 0 });
            var retorno = new SqlParameter("@Retorno", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            comando.Parameters.Add(retorno);
            await comando.ExecuteNonQueryAsync(cancellationToken);

            if ((int)retorno.Value >= 0)
            {
                _conexion = conexion;
                logger.LogInformation("Cerrojo del consumidor obtenido");
                return true;
            }
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }

        await conexion.DisposeAsync();
        return false;
    }

    public async Task ComprobarVigenteAsync(CancellationToken cancellationToken)
    {
        if (_conexion is not { State: ConnectionState.Open } conexion)
        {
            throw new CerrojoPerdidoException("La conexión del cerrojo del consumidor no está abierta.");
        }

        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT APPLOCK_MODE('public', @Recurso, 'Session')";
            comando.Parameters.Add(new SqlParameter("@Recurso", SqlDbType.NVarChar, 255) { Value = Recurso });
            var modo = (string?)await comando.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(modo, "Exclusive", StringComparison.Ordinal))
            {
                throw new CerrojoPerdidoException($"El cerrojo del consumidor no está vigente (modo {modo}).");
            }
        }
        catch (SqlException ex)
        {
            throw new CerrojoPerdidoException("No se pudo comprobar el cerrojo del consumidor.", ex);
        }
    }

    public async ValueTask LiberarAsync()
    {
        if (_conexion is null)
        {
            return;
        }

        try
        {
            if (_conexion.State == ConnectionState.Open)
            {
                await using var comando = _conexion.CreateCommand();
                comando.CommandText = "sys.sp_releaseapplock";
                comando.CommandType = CommandType.StoredProcedure;
                comando.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = Recurso });
                comando.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.VarChar, 32) { Value = "Session" });
                await comando.ExecuteNonQueryAsync();
            }
        }
        catch (SqlException ex)
        {
            // Cerrar la sesión libera el cerrojo igualmente.
            logger.LogWarning(ex, "No se pudo liberar explícitamente el cerrojo del consumidor");
        }
        finally
        {
            await _conexion.DisposeAsync();
            _conexion = null;
        }
    }

    public ValueTask DisposeAsync() => LiberarAsync();
}
