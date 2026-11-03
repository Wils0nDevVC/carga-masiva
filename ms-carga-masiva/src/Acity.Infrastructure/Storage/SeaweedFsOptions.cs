namespace Acity.Infrastructure.Storage;

/// <summary>Filer HTTP de SeaweedFS (variable de entorno <c>SeaweedFs__FilerUrl</c>).</summary>
public sealed class SeaweedFsOptions
{
    public const string Seccion = "SeaweedFs";

    public string FilerUrl { get; set; } = string.Empty;

    public bool EsValida() => Uri.TryCreate(FilerUrl, UriKind.Absolute, out _);
}
