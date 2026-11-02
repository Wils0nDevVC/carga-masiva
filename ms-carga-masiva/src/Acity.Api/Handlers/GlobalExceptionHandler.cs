using Acity.Common.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Acity.Api.Handlers;

/// <summary>Manejo global de excepciones: siempre responde con Response&lt;T&gt; y Code igual al estado HTTP.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Response<object> response;

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
            response = Response<object>.Fail(StatusCodes.Status400BadRequest, "Error de validación", errors);
        }
        else
        {
            logger.LogError(exception, "Excepción no controlada en {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            response = Response<object>.Fail(StatusCodes.Status500InternalServerError, "Error interno del servidor");
        }

        httpContext.Response.StatusCode = response.Code;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
