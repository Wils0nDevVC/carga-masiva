namespace Acity.Application.Interfaces.Services;

/// <summary>Exclusividad del consumidor de cargas (constitución §9; F001 R-02): un solo procesador activo.</summary>
public interface ICerrojoConsumidor
{
    /// <summary>Intenta obtener el cerrojo sin espera. <c>false</c> si otra instancia lo tiene.</summary>
    Task<bool> IntentarObtenerAsync(CancellationToken cancellationToken);

    /// <summary>Lanza <see cref="CerrojoPerdidoException"/> si el cerrojo ya no está vigente.</summary>
    Task ComprobarVigenteAsync(CancellationToken cancellationToken);

    ValueTask LiberarAsync();
}

/// <summary>El consumidor perdió el cerrojo: debe detenerse sin confirmar el mensaje en curso.</summary>
public sealed class CerrojoPerdidoException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);
