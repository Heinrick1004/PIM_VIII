using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Autenticação");
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login");
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization();
        return group;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        IUsuarioRepository users,
        IPasswordHasher<Usuario> passwordHasher,
        JwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["O corpo da requisição é obrigatório."]
            });
        }

        var email = request.Email?.Trim().ToLowerInvariant();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            errors["email"] = ["Informe um e-mail válido."];
        }

        if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length > PasswordRules.MaximumLength)
        {
            errors["senha"] = ["Informe uma senha válida."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await users.GetByEmailAsync(email!, cancellationToken);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Senha!);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var passwordHash = passwordHasher.HashPassword(user, request.Senha!);
            user.UpdatePasswordHash(passwordHash);
            await users.UpdatePasswordHashAsync(user.Id, passwordHash, cancellationToken);
        }

        var (token, expiresAtUtc) = tokenService.Create(user);
        return Results.Ok(new AuthenticationResponse(
            token,
            "Bearer",
            expiresAtUtc,
            new PublicUserResponse(user.Id, user.Nome, user.Email, user.Perfil)));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IUsuarioRepository users,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new PublicUserResponse(user.Id, user.Nome, user.Email, user.Perfil));
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Credenciais inválidas.",
            detail: "E-mail ou senha incorretos.");
}
