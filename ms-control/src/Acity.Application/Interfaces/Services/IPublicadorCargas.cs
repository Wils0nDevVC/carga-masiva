namespace Acity.Application.Interfaces.Services;

/// <summary>Publicación del mensaje de procesamiento en la cola <c>carga_masiva</c> (README §2, §3 paso 2).</summary>
public interface IPublicadorCargas
{
    /// <summary>
    /// Completa solo cuando el intermediario confirma la recepción. Una excepción no prueba que el mensaje no llegó
    /// (p. ej. tras un tiempo de espera).
    /// </summary>
    Task PublicarAsync(MensajeCarga mensaje, CancellationToken cancellationToken);
}
