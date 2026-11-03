using FluentValidation;

namespace Acity.Application.Cargas.Queries.ObtenerDetalleCarga;

public sealed class ObtenerDetalleCargaQueryValidator : AbstractValidator<ObtenerDetalleCargaQuery>
{
    public ObtenerDetalleCargaQueryValidator()
    {
        RuleFor(q => q.IdCarga).GreaterThan(0).WithMessage("El identificador de la carga debe ser mayor que 0.");
    }
}
