using Acity.Api.Consumers;
using Acity.Api.Handlers;
using Acity.Application;
using Acity.Application.Cargas;
using Acity.Common.Helpers;
using Acity.Infrastructure;
using Microsoft.Extensions.Options;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Procesamiento (límite de intentos, F001 R-02 punto 5) y consumidor de carga_masiva (BackgroundService, sin Worker).
    builder.Services.AddOptions<ProcesamientoOptions>()
        .Bind(builder.Configuration.GetSection(ProcesamientoOptions.Seccion))
        .Validate(o => o.MaxIntentos > 0, "Procesamiento:MaxIntentos debe ser mayor que 0.")
        .ValidateOnStart();
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ProcesamientoOptions>>().Value);
    builder.Services.AddHostedService<ConsumidorCargasMasivas>();

    var app = builder.Build();

    app.UseExceptionHandler(_ => { });
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var httpContext = statusCodeContext.HttpContext;
        var code = httpContext.Response.StatusCode;
        var message = code switch
        {
            StatusCodes.Status404NotFound => "No encontrado",
            StatusCodes.Status405MethodNotAllowed => "Método no permitido",
            _ => "Error"
        };
        await httpContext.Response.WriteAsJsonAsync(Response<object>.Fail(code, message));
    });
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
