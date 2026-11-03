namespace Acity.Domain.Entities;

/// <summary>Refresh token persistido como hash (Feature 008, Q2). FechaUso null = disponible; con fecha = usado (rotado).</summary>
public class RefreshToken
{
    public long Id { get; set; }

    public int UsuarioId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaExpiracion { get; set; }

    public DateTime? FechaUso { get; set; }
}
