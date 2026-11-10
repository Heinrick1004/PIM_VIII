using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Contracts;

public sealed record LoginRequest(string? Email, string? Senha);

public sealed record AuthenticationResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    PublicUserResponse User);

public sealed record CreateUserRequest(string? Nome, string? Email, string? Senha, PerfilUsuario Perfil);

public sealed record UpdateUserRequest(string? Nome, string? Email, PerfilUsuario Perfil);

public sealed record PublicUserResponse(Guid Id, string Nome, string Email, PerfilUsuario Perfil);

public sealed record PagedUsersResponse(
    IReadOnlyList<PublicUserResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
