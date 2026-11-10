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
            .ToListAsync(cancellationToken);
}
