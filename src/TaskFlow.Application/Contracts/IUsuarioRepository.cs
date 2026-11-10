using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Contracts;

public interface IUsuarioRepository
{
    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task UpdateAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
    Task<int> CountAdministratorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Usuario> Users, int TotalCount)> ListPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
