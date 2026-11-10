using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Authentication;

public sealed class AdminBootstrapper
{
    private readonly IUsuarioRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AdminBootstrapper(
        IUsuarioRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> CreateFirstAdministratorAsync(
        string nome,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordRules.IsValid(password))
        {
            throw new ArgumentException(
                $"A senha deve ter entre {PasswordRules.MinimumLength} e {PasswordRules.MaximumLength} caracteres.",
                nameof(password));
        }

        await using var transaction = await _unitOfWork.BeginSerializableTransactionAsync(cancellationToken);
        if (await _users.CountAdministratorsAsync(cancellationToken) > 0)
        {
            return false;
        }

        if (await _users.EmailExistsAsync(email, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("O e-mail informado já está cadastrado.");
        }

        var user = new Usuario(Guid.NewGuid(), nome, email, "bootstrap", PerfilUsuario.Administrador);
        user.UpdatePasswordHash(_passwordHasher.HashPassword(user, password));
        try
        {
            await _users.AddAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConstraintException exception) when (exception.Kind == PersistenceConstraintKind.Unique)
        {
            throw new InvalidOperationException("O e-mail informado já está cadastrado.", exception);
        }
    }
}
