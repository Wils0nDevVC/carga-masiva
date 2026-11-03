using MediatR;

namespace Acity.Application.Cargas.Commands.RecuperarCargas;

/// <summary>Recuperación de ejecuciones interrumpidas, con el cerrojo y antes de consumir (constitución §9).</summary>
public sealed record RecuperarCargasCommand : IRequest<int>;
