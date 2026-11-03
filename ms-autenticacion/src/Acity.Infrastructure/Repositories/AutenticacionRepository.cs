using Acity.Application.Interfaces.Repositories;
using Acity.Domain.Entities;
using Acity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acity.Infrastructure.Repositories;

public sealed class AutenticacionRepository(AppDbContext context) : IAutenticacionRepository
{
    public Task<UsuarioConRol?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken cancellationToken) =>
        ConRol(context.Set<Usuario>().AsNoTracking().Where(u => u.Email == email), cancellationToken);

    public Task<UsuarioConRol?> ObtenerUsuarioPorIdAsync(int id, CancellationToken cancellationToken) =>
        ConRol(context.Set<Usuario>().AsNoTracking().Where(u => u.Id == id), cancellationToken);

    public async Task<short?> ObtenerRolIdAsync(string nombre, CancellationToken cancellationToken) =>
        await context.Set<Rol>().AsNoTracking()
            .Where(r => r.Nombre == nombre)
            .Select(r => (short?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RefreshToken?> ObtenerRefreshTokenAsync(byte[] hash, CancellationToken cancellationToken) =>
        context.Set<RefreshToken>().AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

    // UPDATE condicional: solo un uso concurrente del mismo token puede afectar la fila.
    public async Task<bool> MarcarUsadoAsync(long id, DateTime ahora, CancellationToken cancellationToken) =>
        await context.Set<RefreshToken>()
            .Where(t => t.Id == id && t.FechaUso == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaUso, ahora), cancellationToken) == 1;

    private Task<UsuarioConRol?> ConRol(IQueryable<Usuario> usuarios, CancellationToken cancellationToken) =>
        usuarios
            .Join(context.Set<Rol>(), u => u.RolId, r => r.Id, (u, r) => new UsuarioConRol(u, r.Nombre))
            .FirstOrDefaultAsync(cancellationToken);
}
