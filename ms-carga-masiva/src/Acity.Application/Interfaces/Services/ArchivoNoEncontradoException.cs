namespace Acity.Application.Interfaces.Services;

/// <summary>El almacenamiento confirmó que el archivo no existe: fallo técnico de recuperación, no se reintenta (constitución §7).</summary>
public sealed class ArchivoNoEncontradoException(string rutaArchivo)
    : Exception($"El archivo {rutaArchivo} no existe en el almacenamiento.")
{
    public string RutaArchivo { get; } = rutaArchivo;
}
