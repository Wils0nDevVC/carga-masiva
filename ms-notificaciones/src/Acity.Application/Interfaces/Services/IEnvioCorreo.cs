namespace Acity.Application.Interfaces.Services;

/// <summary>Envío del correo de notificación con MailKit (README §2, §4).</summary>
public interface IEnvioCorreo
{
    /// <summary>
    /// Completa si el servidor aceptó el correo. Lanza <see cref="EnvioCorreoFallidoException"/> si se agotaron los
    /// reintentos o el error es permanente, y <see cref="DependenciaNoDisponibleException"/> si el circuito está abierto.
    /// </summary>
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancellationToken);
}

/// <summary>Fallo definitivo del envío (reintentos agotados o error permanente): se registra ErrorNotificacion.</summary>
public sealed class EnvioCorreoFallidoException(string motivo, Exception causa) : Exception(motivo, causa)
{
    /// <summary>Motivo para la incidencia, sin credenciales ni datos del servidor.</summary>
    public string Motivo { get; } = motivo;
}
