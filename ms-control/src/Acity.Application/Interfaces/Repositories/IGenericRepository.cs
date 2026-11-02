namespace Acity.Application.Interfaces.Repositories;

public interface IGenericRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> AsQueryable();

    IQueryable<TEntity> AsNoTracking();

    ValueTask<TEntity?> FindAsync(params object[] keyValues);

    Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Delete(TEntity entity);
}
