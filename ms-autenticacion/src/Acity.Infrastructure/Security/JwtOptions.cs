using System.Text;

namespace Acity.Infrastructure.Security;

/// <summary>
/// Emisión del JWT (constitución §9; Feature 008). Clave, emisor y audiencia son los mismos que valida Control.
/// La clave llega solo por entorno (<c>Jwt__Clave</c>).
/// </summary>
public sealed class JwtOptions
{
    public const string Seccion = "Jwt";

    /// <summary>HS256 exige una clave de al menos 256 bits.</summary>
    public const int LongitudMinimaClaveBytes = 32;

    public string Clave { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    /// <summary>Vigencia del token de acceso (60 minutos por defecto; decisión del usuario).</summary>
    public int VigenciaAccesoMinutos { get; set; } = 60;

    /// <summary>Vigencia del refresh token (1 hora por defecto; decisión Q1).</summary>
    public int VigenciaRefreshMinutos { get; set; } = 60;

    public bool EsValida() =>
        Encoding.UTF8.GetByteCount(Clave) >= LongitudMinimaClaveBytes
        && !string.IsNullOrWhiteSpace(Emisor)
        && !string.IsNullOrWhiteSpace(Audiencia)
        && VigenciaAccesoMinutos > 0
        && VigenciaRefreshMinutos > 0;
}
