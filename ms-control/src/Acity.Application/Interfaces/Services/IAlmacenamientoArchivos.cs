namespace Acity.Application.Interfaces.Services;

/// <summary>Almacenamiento de los Excel recibidos (SeaweedFS; README §2).</summary>
public interface IAlmacenamientoArchivos
{
    /// <summary>
    /// Guarda el contenido en <paramref name="rutaArchivo"/> (<c>seaweed://…</c>), creando o sobrescribiendo.
    /// <paramref name="abrirContenido"/> se invoca en cada intento para obtener un flujo nuevo.
    /// </summary>
    Task SubirAsync(string rutaArchivo, Func<Stream> abrirContenido, CancellationToken cancellationToken);

    /// <summary>
    /// Descarga el archivo para la consulta de contenido (Feature 010). Lanza <see cref="ArchivoNoEncontradoException"/>
    /// si no existe en la ruta y <see cref="AlmacenamientoNoDisponibleException"/> si el almacenamiento no responde.
    /// </summary>
    Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken cancellationToken);
}
