using Acity.Application.Cargas.Queries.Dtos;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Queries.ObtenerDetalleCarga;

public sealed record ObtenerDetalleCargaQuery(long IdCarga, int UsuarioId, bool EsAdministrador) : IRequest<Response<CargaDetalleDto>>;
