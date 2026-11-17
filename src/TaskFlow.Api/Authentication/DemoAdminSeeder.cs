using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Authentication;

public sealed class DemoAdminSeeder
{
    private readonly IUsuarioRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public DemoAdminSeeder(
        IUsuarioRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> EnsureAdministratorAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        await using var transaction = await _unitOfWork.BeginSerializableTransactionAsync(cancellationToken);

        var existing = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Perfil != PerfilUsuario.Administrador)
            {
                throw new InvalidOperationException(
                    $"O e-mail {normalizedEmail} j\u00e1 existe com um perfil diferente de administrador; nenhum dado foi alterado.");
            }

            return false;
        }

        if (!PasswordRules.IsValid(password))
        {
            throw new ArgumentException(
                $"A senha deve ter entre {PasswordRules.MinimumLength} e {PasswordRules.MaximumLength} caracteres.",
                nameof(password));
        }

        var user = new Usuario(Guid.NewGuid(), name, normalizedEmail, "bootstrap", PerfilUsuario.Administrador);
        user.UpdatePasswordHash(_passwordHasher.HashPassword(user, password));
        await _users.AddAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
