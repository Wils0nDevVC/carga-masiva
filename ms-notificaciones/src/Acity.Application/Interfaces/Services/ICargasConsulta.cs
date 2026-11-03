namespace Acity.Application.Interfaces.Services;

/// <summary>Lectura del estado de la carga y registro de la incidencia de fallo de correo.</summary>
public interface ICargasConsulta
{
    /// <summary>Estado actual de la carga, o <c>null</c> si no existe.</summary>
    Task<string?> ObtenerEstadoAsync(long idCarga, CancellationToken cancellationToken);

    /// <summary>Inserta una incidencia ErrorNotificacion vinculada a la carga. Sin reintentos.</summary>
    Task RegistrarErrorNotificacionAsync(long idCarga, string motivo, CancellationToken cancellationToken);
}
