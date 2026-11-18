using System.Text.Json.Serialization;

namespace TaskFlow.Desktop;

internal sealed record LoginRequest(string Email, string Senha);
internal sealed record UserDto(Guid Id, string Nome, string Email, string Perfil);
internal sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc, UserDto User);
internal sealed record TaskDto(Guid Id, string Titulo, string? Descricao, string Status, string Prioridade,
    DateTime CriadaEmUtc, DateTime? PrazoUtc, Guid CriadorId, Guid? ResponsavelId, string RowVersion)
{
    public override string ToString() => $"{Titulo} — {Status}";
}
internal sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
internal sealed record HistoryDto(Guid Id, Guid TarefaId, Guid UsuarioId, string Acao, DateTime DataUtc, string? Descricao);
internal sealed record HistoryPageDto(IReadOnlyList<HistoryDto> Items, int Page, int PageSize, int TotalCount);
internal sealed record StatusRequest(string Status, string RowVersion);
