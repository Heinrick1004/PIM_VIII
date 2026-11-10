using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Contracts;

public interface IUsuarioRepository
{
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default);
}
