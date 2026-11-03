using System.Security.Cryptography;
using System.Text;
using Acity.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Acity.Infrastructure.Security;

/// <summary>
/// Token de acceso: JWT HS256 con sub (Usuario.Id) y role, compatible con la validación de Control (F005).
/// Refresh token: 32 bytes aleatorios en Base64Url; no es un JWT, por lo que Control no lo acepta como acceso.
/// </summary>
public sealed class EmisorTokens(IOptions<JwtOptions> options) : IEmisorTokens
{
    private readonly JwtOptions _opciones = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiraEn) CrearTokenAcceso(int usuarioId, string rol)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.VigenciaAccesoMinutos);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Emisor,
            Audience = _opciones.Audiencia,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = usuarioId.ToString(),
                ["role"] = rol
            },
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)), SecurityAlgorithms.HmacSha256)
        };

        return (_handler.CreateToken(descriptor), expira);
    }

    public (string Valor, byte[] Hash, DateTime ExpiraEn) CrearRefreshToken()
    {
        var valor = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (valor, CalcularHash(valor), DateTime.UtcNow.AddMinutes(_opciones.VigenciaRefreshMinutos));
    }

    public byte[] CalcularHash(string valor) => SHA256.HashData(Encoding.UTF8.GetBytes(valor));
}
