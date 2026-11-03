namespace Acity.Domain.Entities;

/// <summary>
/// Usuario persistido, solo con lo que Control necesita (Feature 005, FR-009): el identificador del claim <c>sub</c>
/// y el correo del mensaje de carga. PasswordHash y RolId no se mapean (mínimo privilegio; el rol llega en el token).
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;
}
