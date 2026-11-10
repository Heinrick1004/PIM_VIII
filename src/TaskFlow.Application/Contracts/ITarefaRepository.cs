using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Contracts;

public interface ITarefaRepository
{
    Task AddAsync(Tarefa tarefa, CancellationToken cancellationToken = default);
    Task<Tarefa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tarefa>> ListAsync(
        int skip,
        int take,
        StatusTarefa? status = null,
        Guid? responsavelId = null,
        string? titulo = null,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Tarefa tarefa, CancellationToken cancellationToken = default);
}
