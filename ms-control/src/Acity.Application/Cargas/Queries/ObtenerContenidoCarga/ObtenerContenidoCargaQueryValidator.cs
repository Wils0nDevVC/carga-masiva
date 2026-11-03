using FluentValidation;

namespace Acity.Application.Cargas.Queries.ObtenerContenidoCarga;

public sealed class ObtenerContenidoCargaQueryValidator : AbstractValidator<ObtenerContenidoCargaQuery>
{
    public ObtenerContenidoCargaQueryValidator()
    {
        RuleFor(q => q.IdCarga).GreaterThan(0).WithMessage("El identificador de la carga debe ser mayor que 0.");
    }
}
