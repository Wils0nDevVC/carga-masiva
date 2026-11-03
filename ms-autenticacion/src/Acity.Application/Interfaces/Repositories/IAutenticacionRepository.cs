using Acity.Domain.Entities;

namespace Acity.Application.Interfaces.Repositories;

/// <summary>Consultas de autenticación y la marca atómica de uso del refresh token (rotación, Q3).</summary>
public interface IAutenticacionRepository
{
    Task<UsuarioConRol?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken cancellationToken);

    Task<UsuarioConRol?> ObtenerUsuarioPorIdAsync(int id, CancellationToken cancellationToken);

    Task<short?> ObtenerRolIdAsync(string nombre, CancellationToken cancellationToken);

    Task<RefreshToken?> ObtenerRefreshTokenAsync(byte[] hash, CancellationToken cancellationToken);

    /// <summary>Marca el token como usado solo si sigue disponible; false si otra solicitud lo usó antes.</summary>
    Task<bool> MarcarUsadoAsync(long id, DateTime ahora, CancellationToken cancellationToken);
}

public sealed record UsuarioConRol(Usuario Usuario, string Rol);
