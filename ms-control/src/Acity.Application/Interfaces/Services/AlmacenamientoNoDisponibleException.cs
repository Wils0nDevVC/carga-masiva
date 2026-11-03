namespace Acity.Application.Interfaces.Services;

/// <summary>El almacenamiento está caído o inaccesible tras los reintentos o con el circuito abierto (Q2 → 503).</summary>
public sealed class AlmacenamientoNoDisponibleException(Exception inner)
    : Exception("El almacenamiento de archivos no está disponible.", inner);
