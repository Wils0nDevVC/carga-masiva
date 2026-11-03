using FluentValidation;

namespace Acity.Application.Autenticacion.Commands.IniciarSesion;

/// <summary>Solo presencia: no se infieren longitudes ni formato (constitución §4).</summary>
public sealed class IniciarSesionCommandValidator : AbstractValidator<IniciarSesionCommand>
{
    public IniciarSesionCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("El correo es obligatorio.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
