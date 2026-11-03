using System.Text;

namespace Acity.Api.Options;

/// <summary>
/// Validación del JWT emitido por Autenticación (constitución §9: Control valida también el token).
/// La clave llega solo por entorno (<c>Jwt__Clave</c>); emisor y audiencia son los mismos para Autenticación, Gateway y Control.
/// </summary>
public sealed class JwtOptions
{
    public const string Seccion = "Jwt";

    /// <summary>HS256 exige una clave de al menos 256 bits.</summary>
    public const int LongitudMinimaClaveBytes = 32;

    public string Clave { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    public bool EsValida() =>
        Encoding.UTF8.GetByteCount(Clave) >= LongitudMinimaClaveBytes
        && !string.IsNullOrWhiteSpace(Emisor)
        && !string.IsNullOrWhiteSpace(Audiencia);
}
