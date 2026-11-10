using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Infrastructure.Data;

public sealed class TaskFlowUnitOfWork : IUnitOfWork
{
    private readonly TaskFlowDbContext _context;

    public TaskFlowUnitOfWork(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new TaskFlowUnitOfWorkTransaction(transaction);
    }

    private sealed class TaskFlowUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public TaskFlowUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
