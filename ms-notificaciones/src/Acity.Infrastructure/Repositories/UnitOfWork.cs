using Acity.Application.Interfaces.Repositories;
using Acity.Infrastructure.Persistence;

namespace Acity.Infrastructure.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
