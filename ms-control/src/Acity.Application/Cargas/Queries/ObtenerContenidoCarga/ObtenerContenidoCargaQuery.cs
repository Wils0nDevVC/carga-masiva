using Acity.Application.Cargas.Queries.Dtos;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Queries.ObtenerContenidoCarga;

public sealed record ObtenerContenidoCargaQuery(long IdCarga, int UsuarioId, bool EsAdministrador) : IRequest<Response<ContenidoArchivoDto>>;
