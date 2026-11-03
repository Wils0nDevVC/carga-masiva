namespace Acity.Application.Interfaces.Services;

/// <summary>Almacenamiento de los Excel recibidos (SeaweedFS; README §2).</summary>
public interface IAlmacenamientoArchivos
{
    /// <summary>
    /// Guarda el contenido en <paramref name="rutaArchivo"/> (<c>seaweed://…</c>), creando o sobrescribiendo.
    /// <paramref name="abrirContenido"/> se invoca en cada intento para obtener un flujo nuevo.
    /// </summary>
    Task SubirAsync(string rutaArchivo, Func<Stream> abrirContenido, CancellationToken cancellationToken);
}
