using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Acity.Api.Filters;

/// <summary>
/// Impide que el enlace de modelos lea el formulario antes de la acción. Así el multipart lo lee solo el controlador
/// y un cuerpo mayor que el límite llega al manejo global como 413 con Response&lt;T&gt;, en lugar de un ProblemDetails
/// automático (un segundo formato de error, constitución §5).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SinEnlaceDeFormularioAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factorias = context.ValueProviderFactories;
        factorias.RemoveType<FormValueProviderFactory>();
        factorias.RemoveType<FormFileValueProviderFactory>();
        factorias.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
