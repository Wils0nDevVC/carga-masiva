namespace Acity.Application.Interfaces.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="trabajo"/> en una transacción de base de datos y la confirma al terminar;
    /// si el trabajo lanza, la transacción se revierte. Sin reintentos.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> trabajo, CancellationToken cancellationToken = default);
}
