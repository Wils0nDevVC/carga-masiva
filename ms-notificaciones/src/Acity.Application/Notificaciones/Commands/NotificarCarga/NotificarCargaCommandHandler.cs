using Acity.Application.Interfaces.Services;
using Acity.Application.Notificaciones.Correo;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Notificaciones.Commands.NotificarCarga;

/// <summary>
/// Notifica una carga Finalizado (README §2, §3 paso 4; constitución §8 v3.3.0): consulta el estado, envía el correo
/// aprobado y, solo tras el envío, pasa la carga a Notificado. Las excepciones que suben dejan el mensaje sin confirmar.
/// </summary>
public sealed class NotificarCargaCommandHandler(
    ICargasConsulta cargas,
    IEnvioCorreo correo,
    IProcedimientosCarga procedimientos,
    ConfiguracionCorreo configuracion,
    ILogger<NotificarCargaCommandHandler> logger) : IRequestHandler<NotificarCargaCommand, ResultadoNotificacion>
{
    private const string Finalizado = "Finalizado";

    public async Task<ResultadoNotificacion> Handle(NotificarCargaCommand request, CancellationToken cancellationToken)
    {
        // En una reentrega (incluida la de un correo ya enviado sin Notificado guardado) se consulta primero el estado.
        var estado = await cargas.ObtenerEstadoAsync(request.IdCarga, CancellationToken.None);
        if (estado != Finalizado)
        {
            logger.LogInformation("Mensaje descartado: la carga {IdCarga} está {Estado}", request.IdCarga, estado ?? "inexistente");
            return ResultadoNotificacion.Descartada;
        }

        // Contenido del correo (Feature 016, sustituye la Q1 de la 007): archivo, periodos e incidencias. Si la lectura
        // falla, la excepción sube y el mensaje no se confirma, igual que al leer el estado.
        var resumen = await cargas.ObtenerResumenAsync(request.IdCarga, CancellationToken.None);
        var mensaje = PlantillaCorreo.Construir(request.IdCarga, request.FechaFin, resumen, configuracion.UrlFrontend);

        try
        {
            await correo.EnviarAsync(request.Usuario, mensaje.Asunto, mensaje.Texto, mensaje.Html, CancellationToken.None);
        }
        catch (EnvioCorreoFallidoException ex)
        {
            // Reintentos agotados o error permanente (Q3): ErrorNotificacion, la carga conserva Finalizado y el mensaje se
            // confirma. Si la incidencia no se puede guardar, la excepción sube y el mensaje no se confirma.
            logger.LogError(ex, "Correo de la carga {IdCarga} no enviado: {Motivo}", request.IdCarga, ex.Motivo);
            await cargas.RegistrarErrorNotificacionAsync(request.IdCarga, ex.Motivo, CancellationToken.None);
            return ResultadoNotificacion.EnvioFallido;
        }

        try
        {
            if (!await procedimientos.CambiarEstadoAsync(request.IdCarga, "Notificado", Finalizado, CancellationToken.None))
            {
                logger.LogWarning("Correo de la carga {IdCarga} enviado, pero la carga ya no estaba Finalizado", request.IdCarga);
            }
        }
        catch (Exception ex)
        {
            // Q2: sin ErrorNotificacion ni Fallido; el mensaje no se confirma y la reentrega consulta el estado
            // (puede repetirse el correo: limitación documentada).
            logger.LogError(ex, "Correo de la carga {IdCarga} enviado; Notificado no registrado", request.IdCarga);
            throw;
        }

        logger.LogInformation("Carga {IdCarga} notificada", request.IdCarga);
        return ResultadoNotificacion.Notificada;
    }
}
