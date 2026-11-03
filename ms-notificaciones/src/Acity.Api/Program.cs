using Acity.Api.Consumers;
using Acity.Api.Handlers;
using Acity.Application;
using Acity.Common.Helpers;
using Acity.Infrastructure;
using Acity.Application.Notificaciones.Correo;
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
    builder.Services.AddHostedService<ConsumidorNotificaciones>();

    // "Ver detalle" en el correo (Feature 016, Q3): URL pública del frontend, opcional; solo http(s) absoluta.
    var urlFrontend = builder.Configuration["Notificaciones:UrlFrontend"];
    var urlValida = Uri.TryCreate(urlFrontend, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    if (!string.IsNullOrWhiteSpace(urlFrontend) && !urlValida)
    {
        Log.Warning("Notificaciones:UrlFrontend no es una URL http(s) absoluta; el correo se enviará sin enlace al detalle");
    }
    builder.Services.AddSingleton(new ConfiguracionCorreo(urlValida ? urlFrontend : null));

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
