namespace Acity.Api.Options;

/// <summary>
/// Credenciales de los usuarios iniciales, solo por entorno (<c>UsuariosIniciales__Administrador__Email</c>, etc.).
/// Sin ellas el servicio no arranca. No se exige longitud mínima de contraseña (no se infieren reglas).
/// </summary>
public sealed class UsuariosInicialesOptions
{
    public const string Seccion = "UsuariosIniciales";

    public CredencialInicial Administrador { get; set; } = new();

    public CredencialInicial Operador { get; set; } = new();

    public bool EsValida() => Administrador.EsValida() && Operador.EsValida();
}

public sealed class CredencialInicial
{
    public string Email { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public bool EsValida() => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Clave);
}
