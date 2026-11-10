using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Repositories;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly TaskFlowDbContext _context;

    public UsuarioRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        await _context.Usuarios.AddAsync(usuario, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePasswordHashAsync(
        Guid id,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        var updatedCount = await _context.Usuarios
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.PasswordHash, passwordHash),
                cancellationToken);
        if (updatedCount != 1)
        {
            throw new InvalidOperationException("O usuário deixou de existir durante a atualização do hash.");
        }
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        Guid? excludingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Usuarios.AsNoTracking()
            .AnyAsync(x => x.Email == normalized && (!excludingUserId.HasValue || x.Id != excludingUserId.Value), cancellationToken);
    }

    public async Task<int> CountAdministratorsAsync(CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AsNoTracking()
            .CountAsync(x => x.Perfil == TaskFlow.Domain.Enums.PerfilUsuario.Administrador, cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AsNoTracking().OrderBy(x => x.Nome).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Usuario> Users, int TotalCount)> ListPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var users = _context.Usuarios.AsNoTracking();
        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users.OrderBy(x => x.Nome)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }
}
