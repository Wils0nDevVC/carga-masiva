using Acity.Application.Interfaces.Repositories;
using Acity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acity.Infrastructure.Repositories;

public class GenericRepository<TEntity>(AppDbContext context) : IGenericRepository<TEntity> where TEntity : class
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public IQueryable<TEntity> AsQueryable() => _set.AsQueryable();

    public IQueryable<TEntity> AsNoTracking() => _set.AsNoTracking();

    public ValueTask<TEntity?> FindAsync(params object[] keyValues) => _set.FindAsync(keyValues);

    public async Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => _set.Update(entity);

    public void Delete(TEntity entity) => _set.Remove(entity);
}
