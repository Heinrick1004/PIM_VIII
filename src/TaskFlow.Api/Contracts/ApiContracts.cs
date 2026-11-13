using System.Text.Json;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Entities;

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

public sealed record CreateTaskRequest(
    string? Titulo,
    string? Descricao,
    PrioridadeTarefa Prioridade,
    DateTime? PrazoUtc,
    Guid? ResponsavelId);

public sealed record UpdateTaskStatusRequest(JsonElement Status = default, string? RowVersion = null);

public sealed record UpdateTaskRequest(
    JsonElement Titulo = default,
    JsonElement Descricao = default,
    JsonElement Prioridade = default,
    JsonElement PrazoUtc = default,
    JsonElement ResponsavelId = default,
    string? RowVersion = null,
    JsonElement Status = default);

public sealed record PublicTaskResponse(
    Guid Id,
    string Titulo,
    string? Descricao,
    StatusTarefa Status,
    PrioridadeTarefa Prioridade,
    DateTime CriadaEmUtc,
    DateTime? PrazoUtc,
    Guid CriadorId,
    Guid? ResponsavelId,
    string RowVersion)
{
    public static PublicTaskResponse From(Tarefa tarefa) =>
        new(
            tarefa.Id,
            tarefa.Titulo,
            tarefa.Descricao,
            tarefa.Status,
            tarefa.Prioridade,
            tarefa.CriadaEmUtc,
            tarefa.PrazoUtc,
            tarefa.CriadorId,
            tarefa.ResponsavelId,
            Convert.ToBase64String(tarefa.RowVersion
                ?? throw new InvalidOperationException("SQL Server did not generate a task row version.")));
}

public sealed record PublicTaskHistoryResponse(
    Guid Id,
    Guid TarefaId,
    Guid UsuarioId,
    string Acao,
    DateTime DataUtc,
    string? Descricao);

public sealed record PagedTaskHistoryResponse(
    IReadOnlyList<PublicTaskHistoryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PagedTasksResponse(
    IReadOnlyList<PublicTaskResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TaskSummaryResponse(int Total, int Pending, int InProgress, int Completed);
