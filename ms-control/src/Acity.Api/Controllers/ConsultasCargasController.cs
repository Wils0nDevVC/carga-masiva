using Acity.Application.Cargas.Queries.ListarCargas;
using Acity.Application.Cargas.Queries.ObtenerContenidoCarga;
using Acity.Application.Cargas.Queries.ObtenerDetalleCarga;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acity.Api.Controllers;

/// <summary>
/// Consultas de seguimiento (README §2, Feature 010). Controlador separado de CargasController para no aplicar el
/// Rate Limiting del endpoint de carga (constitución §9): el polling no consume el límite de subidas.
/// </summary>
[ApiController]
[Route("cargas")]
[Authorize(Roles = "Administrador,Operador")]
public class ConsultasCargasController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var (usuarioId, esAdministrador) = Usuario();
        var respuesta = await mediator.Send(new ListarCargasQuery(usuarioId, esAdministrador), cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObtenerDetalle(long id, CancellationToken cancellationToken)
    {
        var (usuarioId, esAdministrador) = Usuario();
        var respuesta = await mediator.Send(new ObtenerDetalleCargaQuery(id, usuarioId, esAdministrador), cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }

    /// <summary>Filas del Excel original (Q1 = B); errores del archivo según Q2.</summary>
    [HttpGet("{id:long}/contenido")]
    public async Task<IActionResult> ObtenerContenido(long id, CancellationToken cancellationToken)
    {
        var (usuarioId, esAdministrador) = Usuario();
        var respuesta = await mediator.Send(new ObtenerContenidoCargaQuery(id, usuarioId, esAdministrador), cancellationToken);
        return StatusCode(respuesta.Code, respuesta);
    }

    /// <summary>El token ya se validó: sub es un entero positivo (OnTokenValidated).</summary>
    private (int UsuarioId, bool EsAdministrador) Usuario() =>
        (int.Parse(User.FindFirst("sub")!.Value), User.IsInRole("Administrador"));
}
