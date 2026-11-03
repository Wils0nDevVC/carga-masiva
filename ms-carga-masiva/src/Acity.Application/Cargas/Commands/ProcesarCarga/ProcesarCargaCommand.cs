using MediatR;

namespace Acity.Application.Cargas.Commands.ProcesarCarga;

/// <summary>Mensaje de <c>carga_masiva</c> ya validado: la carga a procesar y la ruta que trae el mensaje.</summary>
public sealed record ProcesarCargaCommand(long IdCarga, string? RutaArchivoMensaje) : IRequest<ResultadoProcesamiento>;

public enum ResultadoProcesamiento
{
    /// <summary>La carga se tomó y terminó en Finalizado o Fallido.</summary>
    Procesada,

    /// <summary>El mensaje no correspondía (carga inexistente o no Pendiente): se confirma sin cambios.</summary>
    Descartada
}
