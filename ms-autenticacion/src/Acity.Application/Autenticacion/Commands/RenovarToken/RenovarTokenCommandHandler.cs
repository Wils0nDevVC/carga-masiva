using Acity.Application.Autenticacion.Commands.IniciarSesion;
using Acity.Application.Autenticacion.Dtos;
using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Common.Helpers;
using Acity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Autenticacion.Commands.RenovarToken;

/// <summary>
/// Renovación con rotación (Q3): el refresh token usado queda invalidado y se entrega uno nuevo.
/// Desconocido, usado, vencido, de usuario inexistente o usado en paralelo → 401 genérico.
/// </summary>
public sealed class RenovarTokenCommandHandler(
    IAutenticacionRepository autenticacion,
    IGenericRepository<RefreshToken> refreshTokens,
    IUnitOfWork unitOfWork,
    IEmisorTokens emisorTokens,
    ILogger<RenovarTokenCommandHandler> logger) : IRequestHandler<RenovarTokenCommand, Response<TokensDto>>
{
    public const string MensajeRefreshInvalido = "Refresh token inválido";

    public async Task<Response<TokensDto>> Handle(RenovarTokenCommand request, CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;
        var registro = await autenticacion.ObtenerRefreshTokenAsync(emisorTokens.CalcularHash(request.RefreshToken), cancellationToken);
        if (registro is null)
        {
            return Rechazar("Desconocido");
        }

        if (registro.FechaUso is not null)
        {
            return Rechazar("Usado");
        }

        if (registro.FechaExpiracion <= ahora)
        {
            return Rechazar("Vencido");
        }

        var usuario = await autenticacion.ObtenerUsuarioPorIdAsync(registro.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return Rechazar("UsuarioInexistente");
        }

        if (!await autenticacion.MarcarUsadoAsync(registro.Id, ahora, cancellationToken))
        {
            return Rechazar("Concurrente");
        }

        var tokens = await IniciarSesionCommandHandler.EmitirAsync(usuario, refreshTokens, emisorTokens, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refresh correcto. UsuarioId {UsuarioId}", usuario.Usuario.Id);
        return Response<TokensDto>.Ok(tokens);
    }

    private Response<TokensDto> Rechazar(string motivo)
    {
        logger.LogWarning("Refresh rechazado. Motivo {Motivo}", motivo);
        return Response<TokensDto>.Fail(401, MensajeRefreshInvalido);
    }
}
