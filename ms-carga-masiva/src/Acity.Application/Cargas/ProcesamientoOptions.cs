namespace Acity.Application.Cargas;

/// <summary>Parámetros del procesamiento (variable de entorno <c>Procesamiento__MaxIntentos</c>).</summary>
public sealed class ProcesamientoOptions
{
    public const string Seccion = "Procesamiento";

    /// <summary>Ejecuciones iniciadas o reanudadas permitidas por carga antes de dejarla Fallido (F001 R-02 punto 5).</summary>
    public int MaxIntentos { get; set; } = 3;
}
