namespace TaskFlow.Domain.Entities;

public sealed class HistoricoTarefa
{
    public const int AcaoMaxLength = 80;
    public const int DescricaoMaxLength = 1000;

    public HistoricoTarefa(Guid id, Guid tarefaId, Guid usuarioId, string acao, DateTime dataUtc, string? descricao)
    {
        if (id == Guid.Empty || tarefaId == Guid.Empty || usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Os identificadores do histórico são obrigatórios.");
        }

        if (string.IsNullOrWhiteSpace(acao) || acao.Trim().Length > AcaoMaxLength)
        {
            throw new ArgumentException($"A ação é obrigatória e deve ter até {AcaoMaxLength} caracteres.", nameof(acao));
        }

        if (descricao?.Length > DescricaoMaxLength)
        {
            throw new ArgumentException($"A descrição deve ter até {DescricaoMaxLength} caracteres.", nameof(descricao));
        }

        if (dataUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data do histórico deve estar em UTC.", nameof(dataUtc));
        }

        Id = id;
        TarefaId = tarefaId;
        UsuarioId = usuarioId;
        Acao = acao.Trim();
        DataUtc = dataUtc;
        Descricao = descricao;
    }

    public Guid Id { get; }
    public Guid TarefaId { get; }
    public Guid UsuarioId { get; }
    public string Acao { get; }
    public DateTime DataUtc { get; }
    public string? Descricao { get; }
}
