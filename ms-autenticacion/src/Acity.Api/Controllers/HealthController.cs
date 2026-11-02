using Acity.Common.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Acity.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>Indica que el host arrancó. No consulta la base de datos ni otras dependencias.</summary>
    [HttpGet("/health")]
    public IActionResult Get() => Ok(Response<string>.Ok("Healthy"));
}
