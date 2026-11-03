using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Commands.RegistrarCarga;

/// <summary>
/// Recepción de un Excel. <paramref name="UsuarioId"/> proviene del claim <c>sub</c> del token validado;
/// <paramref name="CantidadArchivos"/> cuenta todos los archivos del formulario.
/// </summary>
public sealed record RegistrarCargaCommand(
    int UsuarioId,
    int CantidadArchivos,
    string? NombreArchivo,
    long Longitud,
    Func<Stream>? AbrirContenido) : IRequest<Response<RegistrarCargaResponse>>;
