using Acity.Application.Autenticacion.Dtos;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Autenticacion.Commands.RenovarToken;

public sealed record RenovarTokenCommand(string RefreshToken) : IRequest<Response<TokensDto>>;
