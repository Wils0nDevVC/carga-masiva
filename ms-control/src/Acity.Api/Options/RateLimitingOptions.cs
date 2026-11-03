namespace Acity.Api.Options;

/// <summary>Rate Limiting del endpoint de carga (README §4; constitución §9: en Control, no en el Gateway).</summary>
public sealed class RateLimitingOptions
{
    public const string Seccion = "RateLimiting";

    public const string PoliticaCarga = "carga";

    public int LimitePorVentana { get; set; } = 10;

    public int VentanaSegundos { get; set; } = 60;

    public bool EsValida() => LimitePorVentana > 0 && VentanaSegundos > 0;
}
