using System.Text;
using Acity.Api.Handlers;
using Acity.Api.Options;
using Acity.Application;
using Acity.Application.Cargas;
using Acity.Common.Helpers;
using Acity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // JWT validado en Control además del Gateway (constitución §9). Contrato de claims: sub (Usuario.Id) y role.
    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection(JwtOptions.Seccion))
        .Validate(o => o.EsValida(), $"Configuración Jwt inválida: Clave (>= {JwtOptions.LongitudMinimaClaveBytes} bytes), Emisor y Audiencia son obligatorios.")
        .ValidateOnStart();
    var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
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
                // sub debe ser el identificador numérico de Usuario; si no lo es, el token no autentica.
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
    builder.Services.AddAuthorization();

    // Tamaño máximo del archivo (10 MiB predeterminado; CargaArchivo__TamanoMaximoBytes). Sin valor válido, no arranca.
    builder.Services.AddOptions<CargaArchivoOptions>()
        .Bind(builder.Configuration.GetSection(CargaArchivoOptions.Seccion))
        .Validate(o => o.TamanoMaximoBytes > 0, "CargaArchivo:TamanoMaximoBytes debe ser mayor que 0.")
        .ValidateOnStart();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<CargaArchivoOptions>>().Value);

    // Límites HTTP: el archivo máximo más el contenido adicional del multipart debe caber; por encima, el servidor
    // corta la lectura (413). El rechazo por tamaño entre el máximo y el margen lo da el validador (400).
    var tamanoMaximo = builder.Configuration.GetSection(CargaArchivoOptions.Seccion).Get<CargaArchivoOptions>()?.TamanoMaximoBytes
        ?? new CargaArchivoOptions().TamanoMaximoBytes;
    var limiteCuerpo = Math.Max(tamanoMaximo, 0) + MargenMultipartBytes;
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = limiteCuerpo);
    builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limiteCuerpo);

    // Rate Limiting: ventana fija por usuario (sub) o, sin usuario, por IP; sin cola; rechazo 429 (cuerpo por UseStatusCodePages).
    builder.Services.AddOptions<RateLimitingOptions>()
        .Bind(builder.Configuration.GetSection(RateLimitingOptions.Seccion))
        .Validate(o => o.EsValida(), "RateLimiting: LimitePorVentana y VentanaSegundos deben ser mayores que 0.")
        .ValidateOnStart();
    var limites = builder.Configuration.GetSection(RateLimitingOptions.Seccion).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(RateLimitingOptions.PoliticaCarga, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limites.LimitePorVentana,
                    Window = TimeSpan.FromSeconds(limites.VentanaSegundos),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddControllers();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseExceptionHandler(_ => { });
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var httpContext = statusCodeContext.HttpContext;
        var code = httpContext.Response.StatusCode;
        var message = code switch
        {
            StatusCodes.Status401Unauthorized => "No autenticado",
            StatusCodes.Status403Forbidden => "Sin permiso",
            StatusCodes.Status404NotFound => "No encontrado",
            StatusCodes.Status405MethodNotAllowed => "Método no permitido",
            StatusCodes.Status413PayloadTooLarge => "El archivo excede el tamaño permitido",
            StatusCodes.Status429TooManyRequests => "Demasiadas solicitudes",
            _ => "Error"
        };
        await httpContext.Response.WriteAsJsonAsync(Response<object>.Fail(code, message));
    });
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
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

public partial class Program
{
    /// <summary>Margen sobre el tamaño máximo para fronteras y cabeceras del multipart (1 MiB).</summary>
    private const long MargenMultipartBytes = 1_048_576;
}
