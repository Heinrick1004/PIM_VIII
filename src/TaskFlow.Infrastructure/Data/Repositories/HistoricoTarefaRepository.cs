using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Repositories;

public sealed class HistoricoTarefaRepository : IHistoricoTarefaRepository
{
    private readonly TaskFlowDbContext _context;

    public HistoricoTarefaRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(HistoricoTarefa historico, CancellationToken cancellationToken = default)
    {
        await _context.HistoricoTarefas.AddAsync(historico, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HistoricoTarefa>> GetByTarefaIdAsync(Guid tarefaId, CancellationToken cancellationToken = default) =>
        await _context.HistoricoTarefas.AsNoTracking()
            .Where(x => x.TarefaId == tarefaId)
            .OrderBy(x => x.DataUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<HistoricoTarefa> History, int TotalCount)> ListPageByTarefaIdAsync(
        Guid tarefaId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, 1_000_000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = _context.HistoricoTarefas.AsNoTracking()
            .Where(x => x.TarefaId == tarefaId);
        var totalCount = await query.CountAsync(cancellationToken);
        var history = await query
            .OrderBy(x => x.DataUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (history, totalCount);
    }
}
