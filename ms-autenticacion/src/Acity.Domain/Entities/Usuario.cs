namespace Acity.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public short RolId { get; set; }
}
