using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Contracts;

public sealed record TaskSummary(int Total, int Pending, int InProgress, int Completed);

public interface ITarefaRepository
{
    Task AddAsync(Tarefa tarefa, CancellationToken cancellationToken = default);
    Task<Tarefa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskSummary> GetSummaryAsync(Guid usuarioId, bool ehAdministrador, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Tarefa> Tasks, int TotalCount)> ListPageAsync(
        int page,
        int pageSize,
        Guid? visibleUserId,
        StatusTarefa? status = null,
        PrioridadeTarefa? prioridade = null,
        Guid? responsavelId = null,
        bool semResponsavel = false,
        string? titulo = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tarefa>> ListAsync(
        int skip,
        int take,
        StatusTarefa? status = null,
        Guid? responsavelId = null,
        string? titulo = null,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Tarefa tarefa, CancellationToken cancellationToken = default);
}
