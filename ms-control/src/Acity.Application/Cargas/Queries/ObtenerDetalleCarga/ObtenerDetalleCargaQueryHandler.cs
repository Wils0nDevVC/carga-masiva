using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Interfaces.Repositories;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Cargas.Queries.ObtenerDetalleCarga;

/// <summary>Detalle de una carga. Inexistente o ajena (Operador) → el mismo 404, sin revelar su existencia.</summary>
public sealed class ObtenerDetalleCargaQueryHandler(ICargasConsulta consulta)
    : IRequestHandler<ObtenerDetalleCargaQuery, Response<CargaDetalleDto>>
{
    public const string MensajeNoExiste = "La carga no existe.";

    public async Task<Response<CargaDetalleDto>> Handle(ObtenerDetalleCargaQuery request, CancellationToken cancellationToken)
    {
        var detalle = await consulta.ObtenerDetalleAsync(request.IdCarga, request.UsuarioId, request.EsAdministrador, cancellationToken);
        return detalle is null
            ? Response<CargaDetalleDto>.Fail(404, MensajeNoExiste)
            : Response<CargaDetalleDto>.Ok(detalle);
    }
}
