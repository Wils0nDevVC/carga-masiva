using MediatR;

namespace Acity.Application.Notificaciones.Commands.NotificarCarga;

/// <summary>Mensaje de <c>notificaciones</c> ya validado (contrato de la F006).</summary>
public sealed record NotificarCargaCommand(long IdCarga, string Usuario, DateTime FechaFin) : IRequest<ResultadoNotificacion>;

public enum ResultadoNotificacion
{
    /// <summary>Correo enviado y carga Notificado (o ya cambiada por otra ejecución).</summary>
    Notificada,

    /// <summary>La carga no estaba Finalizado (incluye ya Notificado): sin correo.</summary>
    Descartada,

    /// <summary>Envío agotado o permanente: ErrorNotificacion registrado; la carga conserva Finalizado.</summary>
    EnvioFallido
}
