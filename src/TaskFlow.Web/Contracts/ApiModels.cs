using System.Text.Json.Serialization;

namespace TaskFlow.Web.Contracts;

public sealed record LoginApiRequest(
    string Email,
    [property: JsonPropertyName("senha")] string Senha);

public sealed record CreateTaskApiRequest(
    string? Titulo,
    string? Descricao,
    PrioridadeTarefa Prioridade,
    DateTime? PrazoUtc,
    Guid? ResponsavelId);

public sealed record UpdateTaskApiRequest(
    string Titulo,
    string? Descricao,
    PrioridadeTarefa Prioridade,
    DateTime? PrazoUtc,
    Guid? ResponsavelId,
    string RowVersion);

public sealed record UpdateTaskStatusApiRequest(StatusTarefa Status, string RowVersion);

public sealed record CreateUserApiRequest(string Nome, string Email, string Senha, PerfilUsuario Perfil);

public sealed record UpdateUserApiRequest(string Nome, string Email, PerfilUsuario Perfil);

public enum PerfilUsuario : byte
{
    Administrador = 1,
    Funcionario = 2
}

public enum PrioridadeTarefa : byte
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

public enum StatusTarefa : byte
{
    Pendente = 1,
    EmAndamento = 2,
    Concluida = 3
}

public sealed record ApiResult<T>(int StatusCode, T? Value, string? Message)
{
    public bool Succeeded => StatusCode is >= 200 and < 300;
}

public sealed record LoginApiResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    PublicUser User);

public sealed record PublicUser(Guid Id, string Nome, string Email, PerfilUsuario Perfil);

public sealed record PagedUsers(
    IReadOnlyList<PublicUser> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PublicTask(
    Guid Id,
    string Titulo,
    string? Descricao,
    StatusTarefa Status,
    PrioridadeTarefa Prioridade,
    DateTime CriadaEmUtc,
    DateTime? PrazoUtc,
    Guid CriadorId,
    Guid? ResponsavelId,
    string RowVersion);

public sealed record PagedTasks(
    IReadOnlyList<PublicTask> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PublicTaskHistory(
    Guid Id,
    Guid TarefaId,
    Guid UsuarioId,
    string Acao,
    DateTime DataUtc,
    string? Descricao);

public sealed record PagedTaskHistory(
    IReadOnlyList<PublicTaskHistory> Items,
    int Page,
    int PageSize,
    int TotalCount);
