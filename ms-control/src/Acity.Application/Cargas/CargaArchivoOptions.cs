namespace Acity.Application.Cargas;

/// <summary>Límites del archivo recibido por Control (constitución §9: 10 MiB predeterminado, configurable por entorno).</summary>
public sealed class CargaArchivoOptions
{
    public const string Seccion = "CargaArchivo";

    /// <summary>Tamaño máximo admitido en bytes (variable de entorno <c>CargaArchivo__TamanoMaximoBytes</c>).</summary>
    public long TamanoMaximoBytes { get; set; } = 10_485_760;
}
