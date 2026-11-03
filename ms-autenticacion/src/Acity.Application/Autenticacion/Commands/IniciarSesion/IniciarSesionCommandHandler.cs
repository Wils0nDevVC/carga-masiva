using Acity.Application.Autenticacion.Dtos;
using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Common.Helpers;
using Acity.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Autenticacion.Commands.IniciarSesion;

/// <summary>
/// Login (README §2 MS0): verifica la contraseña con PasswordHasher y emite el token de acceso y un refresh token.
/// Correo inexistente y contraseña errónea responden igual (401 genérico).
/// </summary>
public sealed class IniciarSesionCommandHandler(
    IAutenticacionRepository autenticacion,
    IGenericRepository<RefreshToken> refreshTokens,
    IUnitOfWork unitOfWork,
    IHashContrasena hashContrasena,
    IEmisorTokens emisorTokens,
    ILogger<IniciarSesionCommandHandler> logger) : IRequestHandler<IniciarSesionCommand, Response<TokensDto>>
{
    public const string MensajeCredencialesInvalidas = "Credenciales inválidas";

    public async Task<Response<TokensDto>> Handle(IniciarSesionCommand request, CancellationToken cancellationToken)
    {
        var encontrado = await autenticacion.ObtenerUsuarioPorEmailAsync(request.Email, cancellationToken);
        if (encontrado is null
            || !hashContrasena.Verificar(encontrado.Usuario, encontrado.Usuario.PasswordHash, request.Password))
        {
            logger.LogWarning("Login rechazado");
            return Response<TokensDto>.Fail(401, MensajeCredencialesInvalidas);
        }

        var tokens = await EmitirAsync(encontrado, refreshTokens, emisorTokens, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Login correcto. UsuarioId {UsuarioId}", encontrado.Usuario.Id);
        return Response<TokensDto>.Ok(tokens);
    }

    /// <summary>Emite el par de tokens y deja el nuevo refresh token (solo su hash) pendiente de guardar.</summary>
    internal static async Task<TokensDto> EmitirAsync(
        UsuarioConRol usuario,
        IGenericRepository<RefreshToken> refreshTokens,
        IEmisorTokens emisorTokens,
        CancellationToken cancellationToken)
    {
        var acceso = emisorTokens.CrearTokenAcceso(usuario.Usuario.Id, usuario.Rol);
        var refresh = emisorTokens.CrearRefreshToken();

        await refreshTokens.CreateAsync(new RefreshToken
        {
            UsuarioId = usuario.Usuario.Id,
            TokenHash = refresh.Hash,
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = refresh.ExpiraEn
        }, cancellationToken);

        return new TokensDto(acceso.Token, acceso.ExpiraEn, refresh.Valor, refresh.ExpiraEn);
    }
}
