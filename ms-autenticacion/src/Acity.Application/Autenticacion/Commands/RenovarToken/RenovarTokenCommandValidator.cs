using FluentValidation;

namespace Acity.Application.Autenticacion.Commands.RenovarToken;

public sealed class RenovarTokenCommandValidator : AbstractValidator<RenovarTokenCommand>
{
    public RenovarTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().WithMessage("El refresh token es obligatorio.");
    }
}
