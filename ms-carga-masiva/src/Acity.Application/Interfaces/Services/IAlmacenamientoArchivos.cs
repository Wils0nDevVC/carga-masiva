namespace Acity.Application.Interfaces.Services;

/// <summary>Recuperación de los Excel almacenados en SeaweedFS (README §2).</summary>
public interface IAlmacenamientoArchivos
{
    /// <summary>
    /// Descarga el archivo de <paramref name="rutaArchivo"/> (<c>seaweed://…</c>). Lanza
    /// <see cref="ArchivoNoEncontradoException"/> si el almacenamiento confirma que no existe (sin reintentos).
    /// </summary>
    Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken cancellationToken);
}
