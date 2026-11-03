namespace Acity.Application.Autenticacion.Dtos;

/// <summary>Respuesta de /auth/login y /auth/refresh (contracts/auth-api.md). Fechas en UTC.</summary>
public sealed record TokensDto(string AccessToken, DateTime ExpiraEn, string RefreshToken, DateTime RefreshExpiraEn);
