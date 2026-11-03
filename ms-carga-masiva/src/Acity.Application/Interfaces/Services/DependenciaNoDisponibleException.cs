namespace Acity.Application.Interfaces.Services;

/// <summary>
/// Una operación protegida fue rechazada por su circuito abierto: no se ejecutó (decisión H3, spec FR-016a). No es un
/// fallo técnico terminal: no marca Fallido, no registra ErrorTecnico ni consume un intento; el mensaje no se confirma
/// y el consumidor espera <see cref="RetryAfter"/> antes de recuperar.
/// </summary>
public sealed class DependenciaNoDisponibleException(string operacion, TimeSpan? retryAfter, Exception causa)
    : Exception($"Operación {operacion} rechazada por circuito abierto.", causa)
{
    public string Operacion { get; } = operacion;

    /// <summary>Tiempo hasta que el circuito pueda cerrarse, si se conoce.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
