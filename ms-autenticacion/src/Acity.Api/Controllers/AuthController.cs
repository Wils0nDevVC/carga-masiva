using Acity.Application.Autenticacion.Commands.IniciarSesion;
using Acity.Application.Autenticacion.Commands.RenovarToken;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Acity.Api.Controllers;

/// <summary>Autenticación (README §2 Microservicio 0): login y renovación del token de acceso. Sin token requerido.</summary>
[ApiController]
[Route("auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] IniciarSesionCommand comando, CancellationToken cancellationToken)
    {
        var respuesta = await mediator.Send(comando, cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RenovarTokenCommand comando, CancellationToken cancellationToken)
    {
        var respuesta = await mediator.Send(comando, cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }
}
