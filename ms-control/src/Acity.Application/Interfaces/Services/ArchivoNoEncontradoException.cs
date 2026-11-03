namespace Acity.Application.Interfaces.Services;

/// <summary>El almacenamiento respondió, pero no hay archivo en la ruta registrada (Feature 010, Q2 → 500).</summary>
public sealed class ArchivoNoEncontradoException(string rutaArchivo)
    : Exception($"No existe el archivo en la ruta registrada: {rutaArchivo}");
