namespace Acity.Application.Interfaces.Services;

/// <summary>Publicación del aviso de carga finalizada en la cola <c>notificaciones</c> (README §2, §3 paso 3).</summary>
public interface IPublicadorNotificaciones
{
    /// <summary>
    /// Completa solo cuando el intermediario confirma la recepción. Una excepción no prueba que el mensaje no llegó.
    /// </summary>
    Task PublicarAsync(MensajeNotificacion mensaje, CancellationToken cancellationToken);
}

/// <summary>Mensaje de la cola <c>notificaciones</c> con el formato del README, sin campos adicionales.</summary>
public sealed record MensajeNotificacion(long IdCarga, string Usuario, DateTime FechaFin);
