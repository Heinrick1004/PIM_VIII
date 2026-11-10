using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Contracts;

public interface IHistoricoTarefaRepository
{
    Task AddAsync(HistoricoTarefa historico, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoricoTarefa>> GetByTarefaIdAsync(Guid tarefaId, CancellationToken cancellationToken = default);
}
