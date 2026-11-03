using MailKit.Security;
using MimeKit;

namespace Acity.Infrastructure.Mail;

/// <summary>Servidor SMTP por variables de entorno (<c>Smtp__*</c>; README §4 "MailKit configurable por variables de entorno").</summary>
public sealed class SmtpOptions
{
    public const string Seccion = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Puerto { get; set; } = 587;

    /// <summary>None, StartTls, SslOnConnect o Auto.</summary>
    public string Seguridad { get; set; } = "StartTls";

    public string Usuario { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public string Remitente { get; set; } = string.Empty;

    public SecureSocketOptions OpcionSeguridad => Seguridad switch
    {
        "None" => SecureSocketOptions.None,
        "StartTls" => SecureSocketOptions.StartTls,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto
    };

    public bool EsValida() =>
        !string.IsNullOrWhiteSpace(Host)
        && Puerto > 0
        && Seguridad is "None" or "StartTls" or "SslOnConnect" or "Auto"
        && (string.IsNullOrEmpty(Usuario) || !string.IsNullOrEmpty(Clave))
        && MailboxAddress.TryParse(Remitente, out _);
}
