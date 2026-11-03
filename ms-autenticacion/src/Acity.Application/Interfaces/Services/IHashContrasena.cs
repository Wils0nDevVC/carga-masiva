using Acity.Domain.Entities;

namespace Acity.Application.Interfaces.Services;

/// <summary>Hash de contraseñas con PasswordHasher de ASP.NET Core Identity (constitución §6).</summary>
public interface IHashContrasena
{
    string Generar(Usuario usuario, string contrasena);

    bool Verificar(Usuario usuario, string hash, string contrasena);
}
