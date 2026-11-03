namespace Acity.Domain.Entities;

/// <summary>
/// Usuario persistido, solo con lo que Carga Masiva necesita (Feature 006, FR-021): el correo del responsable para la
/// notificación. PasswordHash y RolId no se mapean (mínimo privilegio).
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;
}
