using Acity.Api.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Acity.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>Salud del propio Gateway (healthcheck de Compose). No consulta los servicios de destino.</summary>
    [HttpGet("/health")]
    public IActionResult Get() => Ok(Response<string>.Ok("Healthy"));
}
