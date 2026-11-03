using FluentValidation;

namespace Acity.Application.Cargas.Commands.RegistrarCarga;

/// <summary>
/// Validaciones del archivo definidas en las fuentes y decisiones del usuario (spec FR-010 a FR-014):
/// exactamente un archivo, no vacío, extensión .xlsx, nombre dentro de la longitud del esquema y tamaño máximo.
/// No se lee ni valida el contenido del Excel.
/// </summary>
public sealed class RegistrarCargaCommandValidator : AbstractValidator<RegistrarCargaCommand>
{
    /// <summary>Longitud de <c>CargaArchivo.NombreArchivo</c> (NVARCHAR(260)); el nombre nunca se trunca.</summary>
    public const int LongitudMaximaNombre = 260;

    public RegistrarCargaCommandValidator(CargaArchivoOptions opciones)
    {
        RuleFor(c => c.CantidadArchivos)
            .Equal(1)
            .WithMessage("Debe enviarse exactamente un archivo.");

        When(c => c.CantidadArchivos == 1, () =>
        {
            RuleFor(c => c.Longitud)
                .GreaterThan(0)
                .WithMessage("El archivo está vacío.");

            RuleFor(c => c.Longitud)
                .LessThanOrEqualTo(opciones.TamanoMaximoBytes)
                .WithMessage($"El archivo excede el tamaño máximo de {opciones.TamanoMaximoBytes} bytes.");

            RuleFor(c => c.NombreArchivo)
                .NotEmpty()
                .WithMessage("El archivo no tiene nombre.");

            RuleFor(c => c.NombreArchivo)
                .Must(n => n is null || n.Length <= LongitudMaximaNombre)
                .WithMessage($"El nombre del archivo supera {LongitudMaximaNombre} caracteres.");

            RuleFor(c => c.NombreArchivo)
                .Must(n => string.Equals(Path.GetExtension(n), ".xlsx", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Solo se admiten archivos .xlsx.");
        });
    }
}
