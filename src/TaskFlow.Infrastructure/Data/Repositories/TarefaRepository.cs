using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Data.Repositories;

public sealed class TarefaRepository : ITarefaRepository
{
    private readonly TaskFlowDbContext _context;

    public TarefaRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tarefa tarefa, CancellationToken cancellationToken = default)
    {
        await _context.Tarefas.AddAsync(tarefa, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Tarefa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Tarefas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tarefa>> ListAsync(
        int skip,
        int take,
        StatusTarefa? status = null,
        Guid? responsavelId = null,
        string? titulo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var query = _context.Tarefas.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (responsavelId.HasValue)
        {
            query = query.Where(x => x.ResponsavelId == responsavelId.Value);
        }

        if (!string.IsNullOrWhiteSpace(titulo))
        {
            var normalized = titulo.Trim();
            query = query.Where(x => x.Titulo.Contains(normalized));
        }

        return await query
            .OrderBy(x => x.CriadaEmUtc)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tarefa tarefa, CancellationToken cancellationToken = default)
    {
        _context.Entry(tarefa).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
