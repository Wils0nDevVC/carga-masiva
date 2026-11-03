using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Interfaces.Repositories;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Queries.ListarCargas;

/// <summary>Historial de cargas según el rol. Sin cargas: 200 con lista vacía (no es un error).</summary>
public sealed class ListarCargasQueryHandler(ICargasConsulta consulta)
    : IRequestHandler<ListarCargasQuery, Response<IReadOnlyList<CargaResumenDto>>>
{
    public async Task<Response<IReadOnlyList<CargaResumenDto>>> Handle(ListarCargasQuery request, CancellationToken cancellationToken)
        => Response<IReadOnlyList<CargaResumenDto>>.Ok(
            await consulta.ListarAsync(request.UsuarioId, request.EsAdministrador, cancellationToken));
}
