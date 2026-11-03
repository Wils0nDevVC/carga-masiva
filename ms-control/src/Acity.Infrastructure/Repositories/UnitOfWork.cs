using Acity.Application.Interfaces.Repositories;
using Acity.Infrastructure.Persistence;

namespace Acity.Infrastructure.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> trabajo, CancellationToken cancellationToken = default)
    {
        // Sin CommitAsync, desechar la transacción la revierte.
        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);
        await trabajo(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }
}
