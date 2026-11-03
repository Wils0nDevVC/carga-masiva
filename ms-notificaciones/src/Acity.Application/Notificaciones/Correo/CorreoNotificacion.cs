namespace Acity.Application.Notificaciones.Correo;

/// <summary>Correo listo para enviar: dos versiones equivalentes (Feature 016, Q2).</summary>
public sealed record CorreoNotificacion(string Asunto, string Html, string Texto);
