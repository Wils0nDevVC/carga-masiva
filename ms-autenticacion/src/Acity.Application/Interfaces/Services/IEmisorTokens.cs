namespace Acity.Application.Interfaces.Services;

/// <summary>Emisión del token de acceso (JWT) y del refresh token opaco; la criptografía vive en Infrastructure.</summary>
public interface IEmisorTokens
{
    (string Token, DateTime ExpiraEn) CrearTokenAcceso(int usuarioId, string rol);

    /// <summary>Valor opaco para el cliente y su hash SHA-256 (lo único que se persiste).</summary>
    (string Valor, byte[] Hash, DateTime ExpiraEn) CrearRefreshToken();

    byte[] CalcularHash(string valor);
}
