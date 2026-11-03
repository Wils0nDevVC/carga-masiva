using Acity.Application.Autenticacion.Dtos;
using Acity.Common.Helpers;
using MediatR;

namespace Acity.Application.Autenticacion.Commands.IniciarSesion;

public sealed record IniciarSesionCommand(string Email, string Password) : IRequest<Response<TokensDto>>;
