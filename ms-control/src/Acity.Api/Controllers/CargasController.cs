using Acity.Api.Filters;
using Acity.Api.Options;
using Microsoft.AspNetCore.RateLimiting;
using Acity.Application.Cargas.Commands.RegistrarCarga;
using Acity.Common.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acity.Api.Controllers;

/// <summary>Recepción de cargas masivas (README §2 Microservicio 1). Único endpoint de negocio de Control.</summary>
[ApiController]
[Route("cargas")]
[Authorize(Roles = "Administrador,Operador")]
[EnableRateLimiting(RateLimitingOptions.PoliticaCarga)]
public class CargasController(IMediator mediator) : ControllerBase
{
    /// <summary>Recibe un único Excel (.xlsx) por multipart/form-data (campo <c>archivo</c>).</summary>
    [HttpPost]
    [SinEnlaceDeFormulario]
    public async Task<IActionResult> Registrar(CancellationToken cancellationToken)
    {
        // Una solicitud que no es un formulario se trata como "sin archivo" (validación 400).
        IFormFileCollection? archivos = null;
        if (Request.HasFormContentType)
        {
            try
            {
                archivos = (await Request.ReadFormAsync(cancellationToken)).Files;
            }
            catch (InvalidDataException)
            {
                // El lector multipart corta una sección mayor que MultipartBodyLengthLimit.
                return StatusCode(StatusCodes.Status413PayloadTooLarge,
                    Response<object>.Fail(StatusCodes.Status413PayloadTooLarge, "El archivo excede el tamaño permitido"));
            }
        }

        var cantidad = archivos?.Count ?? 0;
        var archivo = cantidad == 1 ? archivos![0] : null;

        // El token ya se validó: sub es un entero positivo (OnTokenValidated).
        var usuarioId = int.Parse(User.FindFirst("sub")!.Value);

        var comando = new RegistrarCargaCommand(
            usuarioId,
            cantidad,
            archivo is null ? null : Path.GetFileName(archivo.FileName),
            archivo?.Length ?? 0,
            archivo is null ? null : archivo.OpenReadStream);

        var respuesta = await mediator.Send(comando, cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }
}
