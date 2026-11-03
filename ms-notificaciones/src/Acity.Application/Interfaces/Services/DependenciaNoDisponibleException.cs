namespace Acity.Application.Interfaces.Services;

/// <summary>
/// Una operación protegida fue rechazada por su circuito abierto (no se ejecutó). No es un fallo definitivo: no se
/// registra incidencia, el mensaje no se confirma y el consumidor espera <see cref="RetryAfter"/> (F006 FR-016a).
/// </summary>
public sealed class DependenciaNoDisponibleException(string operacion, TimeSpan? retryAfter, Exception causa)
    : Exception($"Operación {operacion} rechazada por circuito abierto.", causa)
{
    public string Operacion { get; } = operacion;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
