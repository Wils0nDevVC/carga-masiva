using Acity.Application.Interfaces.Services;
using Acity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Acity.Infrastructure.Security;

/// <summary>PasswordHasher de ASP.NET Core Identity, sin el resto de Identity (constitución §6).</summary>
public sealed class HashContrasena : IHashContrasena
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Generar(Usuario usuario, string contrasena) => _hasher.HashPassword(usuario, contrasena);

    /// <summary>SuccessRehashNeeded cuenta como correcta; el hash no se regraba en el login.</summary>
    public bool Verificar(Usuario usuario, string hash, string contrasena) =>
        _hasher.VerifyHashedPassword(usuario, hash, contrasena) != PasswordVerificationResult.Failed;
}
