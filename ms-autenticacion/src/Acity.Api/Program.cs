using Acity.Api.Handlers;
using Acity.Api.Options;
using Acity.Application.Autenticacion.Commands.CrearUsuariosIniciales;
using Acity.Application;
using Acity.Common.Helpers;
using Acity.Infrastructure;
using MediatR;
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

    // Cuerpo ausente o JSON inválido → 400 con Response<T>, sin ProblemDetails (un solo formato de error, §5).
    builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context =>
        {
            var errores = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Solicitud inválida." : e.ErrorMessage)
                .Distinct()
                .ToList();
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(Response<object>.Fail(StatusCodes.Status400BadRequest, "Error de validación", errores));
        });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Usuarios iniciales (constitución §6): credenciales solo por entorno; sin ellas no arranca.
    builder.Services.AddOptions<UsuariosInicialesOptions>()
        .Bind(builder.Configuration.GetSection(UsuariosInicialesOptions.Seccion))
        .Validate(o => o.EsValida(), "Configuración UsuariosIniciales inválida: Email y Clave de Administrador y Operador son obligatorios.")
        .ValidateOnStart();

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

    // Antes de aceptar solicitudes: un usuario por rol, repetible y sin sobrescribir contraseñas existentes.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var iniciales = scope.ServiceProvider.GetRequiredService<IOptions<UsuariosInicialesOptions>>().Value;
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CrearUsuariosInicialesCommand(
        [
            new UsuarioInicial("Administrador", iniciales.Administrador.Email, iniciales.Administrador.Clave),
            new UsuarioInicial("Operador", iniciales.Operador.Email, iniciales.Operador.Clave)
        ]));
    }

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
