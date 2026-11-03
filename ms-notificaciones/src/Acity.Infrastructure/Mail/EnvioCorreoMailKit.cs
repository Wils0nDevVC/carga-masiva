using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;

namespace Acity.Infrastructure.Mail;

/// <summary>
/// Envía el correo con MailKit (constitución §8): un SmtpClient por envío, HTML con texto plano equivalente, un solo destinatario.
/// Reintentos agotados o error permanente → <see cref="EnvioCorreoFallidoException"/>; circuito abierto →
/// <see cref="DependenciaNoDisponibleException"/>. No registra credenciales ni el servidor.
/// </summary>
public sealed class EnvioCorreoMailKit(
    IOptions<SmtpOptions> options,
    [FromKeyedServices(PipelinesResiliencia.Smtp)] ResiliencePipeline pipeline) : IEnvioCorreo
{
    public async Task EnviarAsync(string destinatario, string asunto, string texto, string html, CancellationToken cancellationToken)
    {
        var o = options.Value;
        MimeMessage mensaje;
        try
        {
            mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(o.Remitente));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            // multipart/alternative: texto plano y HTML equivalentes (Feature 016, Q2).
            mensaje.Body = new BodyBuilder { TextBody = texto, HtmlBody = html }.ToMessageBody();
        }
        catch (ParseException ex)
        {
            throw new EnvioCorreoFallidoException("No se pudo enviar el correo de notificación: dirección de correo inválida.", ex);
        }

        try
        {
            await pipeline.EjecutarAsync(PipelinesResiliencia.Smtp, async ct =>
            {
                using var cliente = new SmtpClient();
                await cliente.ConnectAsync(o.Host, o.Puerto, o.OpcionSeguridad, ct);
                if (!string.IsNullOrEmpty(o.Usuario))
                {
                    await cliente.AuthenticateAsync(o.Usuario, o.Clave, ct);
                }

                await cliente.SendAsync(mensaje, ct);
                await cliente.DisconnectAsync(true, ct);
                return true;
            }, cancellationToken);
        }
        catch (DependenciaNoDisponibleException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var motivo = PipelinesResiliencia.EsSmtpTransitorio(ex)
                ? $"No se pudo enviar el correo de notificación tras los reintentos: {ex.GetType().Name}."
                : ex is SmtpCommandException cmd
                    ? $"No se pudo enviar el correo de notificación: error permanente del servidor ({(int)cmd.StatusCode})."
                    : $"No se pudo enviar el correo de notificación: error permanente ({ex.GetType().Name}).";
            throw new EnvioCorreoFallidoException(motivo, ex);
        }
    }
}
