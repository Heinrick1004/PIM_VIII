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

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Usuarios.AsNoTracking().AnyAsync(x => x.Email == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Usuario>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AsNoTracking().OrderBy(x => x.Nome).ToListAsync(cancellationToken);
}
