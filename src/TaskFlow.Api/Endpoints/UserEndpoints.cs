using TaskFlow.Api.Authentication;
using TaskFlow.Api.Contracts;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .WithTags("Usuários")
            .RequireAuthorization("Administrator");

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest? request,
        IUsuarioRepository users,
        IPasswordHasher<Usuario> passwordHasher,
        CancellationToken cancellationToken)
    {
        var validation = ValidateUser(
            request?.Nome,
            request?.Email,
            request?.Perfil,
            request?.Senha,
            passwordRequired: true);
        if (validation is not null)
        {
            return validation;
        }

        var user = new Usuario(
            Guid.NewGuid(),
            request!.Nome!,
            request.Email!,
            "pending",
            request.Perfil);
        user.UpdatePasswordHash(passwordHasher.HashPassword(user, request.Senha!));

        try
        {
            await users.AddAsync(user, cancellationToken);
        }
        catch (PersistenceConstraintException exception) when (exception.Kind == PersistenceConstraintKind.Unique)
        {
            return Results.Conflict(new { error = "Já existe um usuário com esse e-mail." });
        }

        return Results.Created($"/api/users/{user.Id}", ToResponse(user));
    }

    private static async Task<IResult> ListAsync(
        IUsuarioRepository users,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["A página deve estar entre 1 e 1000000 e pageSize entre 1 e 100."]
            });
        }

        var result = await users.ListPageAsync(page, pageSize, cancellationToken);
        return Results.Ok(new PagedUsersResponse(
            result.Users.Select(ToResponse).ToArray(),
            page,
            pageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        IUsuarioRepository users,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador do usuário é inválido."]
            });
        }

        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateUserRequest? request,
        IUsuarioRepository users,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador do usuário é inválido."]
            });
        }

        var validation = ValidateUser(request?.Nome, request?.Email, request?.Perfil, password: null);
        if (validation is not null)
        {
            return validation;
        }

        await using var transaction = await unitOfWork.BeginSerializableTransactionAsync(cancellationToken);
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (await users.EmailExistsAsync(request!.Email!, id, cancellationToken))
        {
            return Results.Conflict(new { error = "Já existe um usuário com esse e-mail." });
        }

        if (user.Perfil == PerfilUsuario.Administrador
            && request.Perfil != PerfilUsuario.Administrador
            && await users.CountAdministratorsAsync(cancellationToken) <= 1)
        {
            return Results.Conflict(new { error = "Não é possível rebaixar o último administrador." });
        }

        user.UpdateProfile(request.Nome!, request.Email!, request.Perfil);
        try
        {
            await users.UpdateAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PersistenceConstraintException exception) when (exception.Kind == PersistenceConstraintKind.Unique)
        {
            return Results.Conflict(new { error = "Já existe um usuário com esse e-mail." });
        }

        return Results.Ok(ToResponse(user));
    }

    private static IResult? ValidateUser(
        string? nome,
        string? email,
        PerfilUsuario? perfil,
        string? password,
        bool passwordRequired = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > Usuario.NomeMaxLength)
        {
            errors["nome"] = [$"O nome é obrigatório e deve ter até {Usuario.NomeMaxLength} caracteres."];
        }

        var normalizedEmail = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail)
            || normalizedEmail.Length > Usuario.EmailMaxLength
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalizedEmail))
        {
            errors["email"] = ["Informe um e-mail válido."];
        }

        if (!perfil.HasValue || !Enum.IsDefined(perfil.Value))
        {
            errors["perfil"] = ["Informe um perfil válido."];
        }

        if ((passwordRequired || password is not null) && !PasswordRules.IsValid(password))
        {
            errors["senha"] = [$"A senha deve ter entre {PasswordRules.MinimumLength} e {PasswordRules.MaximumLength} caracteres."];
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static PublicUserResponse ToResponse(Usuario user) =>
        new(user.Id, user.Nome, user.Email, user.Perfil);
}
