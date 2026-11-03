using Acity.Application.Cargas.Queries.Dtos;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Queries.ListarCargas;

public sealed record ListarCargasQuery(int UsuarioId, bool EsAdministrador) : IRequest<Response<IReadOnlyList<CargaResumenDto>>>;
