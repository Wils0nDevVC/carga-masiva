namespace Acity.Application.Interfaces.Services;

/// <summary>Mensaje de la cola <c>carga_masiva</c> con el formato del README (§3 paso 2), sin campos adicionales.</summary>
public sealed record MensajeCarga(long IdCarga, string RutaArchivo, string Usuario);
