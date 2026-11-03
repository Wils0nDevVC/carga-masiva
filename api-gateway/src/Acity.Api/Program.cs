using System.Text;
using Acity.Api.Helpers;
using Acity.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Rutas del Gateway (Q1): solo las existentes; sin Rate Limiting, QoS, caché ni descubrimiento (constitución §9).
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: false);

    // JWT con el mismo contrato que valida Control (F005): sin configuración válida no arranca.
    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection(JwtOptions.Seccion))
        .Validate(o => o.EsValida(), $"Configuración Jwt inválida: Clave (>= {JwtOptions.LongitudMinimaClaveBytes} bytes), Emisor y Audiencia son obligatorios.")
        .ValidateOnStart();
    var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();

    // Solo autenticación (Q2): la autorización por rol permanece en Control, que además vuelve a validar el token.
    builder.Services
        .AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Emisor,
                ValidateAudience = true,
                ValidAudience = jwt.Audiencia,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                // sub debe ser el identificador numérico de Usuario, como exige Control.
                OnTokenValidated = context =>
                {
                    var sub = context.Principal?.FindFirst("sub")?.Value;
                    if (!int.TryParse(sub, out var id) || id <= 0)
                    {
                        context.Fail("El claim sub no es un identificador de usuario válido.");
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddControllers();
    builder.Services.AddOcelot(builder.Configuration);

    var app = builder.Build();

    // Errores propios del Gateway (Q3): Response<T> con Code = estado HTTP y sin detalles internos.
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(Response<object>.Fail(StatusCodes.Status500InternalServerError, "Error interno del servidor"));
    }));

    // Ocelot responde sus errores (401, 404, 502) sin cuerpo; aquí se envuelven. Las respuestas de los servicios
    // ya tienen cuerpo, por lo que no se tocan.
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var httpContext = statusCodeContext.HttpContext;
        var code = httpContext.Response.StatusCode;
        var message = code switch
        {
            StatusCodes.Status401Unauthorized => "No autorizado",
            StatusCodes.Status404NotFound => "No encontrado",
            StatusCodes.Status502BadGateway => "Servicio no disponible",
            _ => "Error"
        };
        await httpContext.Response.WriteAsJsonAsync(Response<object>.Fail(code, message));
    });

    // /health del propio Gateway se resuelve antes de Ocelot; el resto lo reenvía Ocelot.
    app.UseRouting();
    // UseEndpoints explícito (ASP0014): con rutas de nivel superior el endpoint se ejecutaría al final, después de
    // Ocelot, que es terminal; así /health responde aquí y nunca se reenvía.
#pragma warning disable ASP0014
    app.UseEndpoints(endpoints => endpoints.MapControllers());
#pragma warning restore ASP0014
    app.UseAuthentication();
    await app.UseOcelot();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "El servicio terminó de forma inesperada");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
