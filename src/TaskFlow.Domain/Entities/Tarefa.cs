using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class Tarefa
{
    public const int TituloMaxLength = 200;
    private byte[]? _rowVersion;

    public Tarefa(
        Guid id,
        string titulo,
        string? descricao,
        StatusTarefa status,
        PrioridadeTarefa prioridade,
        DateTime criadaEmUtc,
        DateTime? prazoUtc,
        Guid criadorId,
        Guid? responsavelId,
        byte[]? rowVersion = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador da tarefa é obrigatório.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(titulo) || titulo.Trim().Length > TituloMaxLength)
        {
            throw new ArgumentException($"O título é obrigatório e deve ter até {TituloMaxLength} caracteres.", nameof(titulo));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Status de tarefa inválido.");
        }

        if (!Enum.IsDefined(prioridade))
        {
            throw new ArgumentOutOfRangeException(nameof(prioridade), "Prioridade de tarefa inválida.");
        }

        if (criadaEmUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data de criação deve estar em UTC.", nameof(criadaEmUtc));
        }

        if (prazoUtc.HasValue && prazoUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O prazo deve estar em UTC.", nameof(prazoUtc));
        }

        if (criadorId == Guid.Empty || responsavelId == Guid.Empty)
        {
            throw new ArgumentException("Os identificadores de usuário não podem ser vazios.");
        }

        if (rowVersion is not null && rowVersion.Length != 8)
        {
            throw new ArgumentException("A versão do SQL Server deve conter exatamente 8 bytes.", nameof(rowVersion));
        }

        Id = id;
        Titulo = titulo.Trim();
        Descricao = descricao;
        Status = status;
        Prioridade = prioridade;
        CriadaEmUtc = criadaEmUtc;
        PrazoUtc = prazoUtc;
        CriadorId = criadorId;
        ResponsavelId = responsavelId;
        _rowVersion = rowVersion?.ToArray();
    }

    public Guid Id { get; }
    public string Titulo { get; private set; }
    public string? Descricao { get; private set; }
    public StatusTarefa Status { get; private set; }
    public PrioridadeTarefa Prioridade { get; private set; }
    public DateTime CriadaEmUtc { get; }
    public DateTime? PrazoUtc { get; private set; }
    public Guid CriadorId { get; }
    public Guid? ResponsavelId { get; private set; }
    /// <summary>
    /// Token atribuído pelo SQL Server; permanece nulo antes da persistência.
    /// A cópia impede que o chamador modifique o token da entidade.
    /// </summary>
    public byte[]? RowVersion => _rowVersion?.ToArray();

    public void SetRowVersion(byte[]? rowVersion)
    {
        if (rowVersion is null)
        {
            _rowVersion = null;
            return;
        }

        if (rowVersion.Length != 8)
        {
            throw new ArgumentException("A versão do SQL Server deve conter exatamente 8 bytes.", nameof(rowVersion));
        }

        _rowVersion = rowVersion.ToArray();
    }

    /// <summary>
    /// Retorna o registro que o caso de uso deve persistir junto com a tarefa,
    /// na mesma transação. Uma operação sem mudança retorna nulo.
    /// </summary>
    public HistoricoTarefa? AlterarTitulo(string titulo, Guid usuarioId, DateTime dataUtc)
    {
        if (string.IsNullOrWhiteSpace(titulo) || titulo.Trim().Length > TituloMaxLength)
        {
            throw new ArgumentException($"O título é obrigatório e deve ter até {TituloMaxLength} caracteres.", nameof(titulo));
        }

        ValidarAutorEData(usuarioId, dataUtc);
        var novoTitulo = titulo.Trim();
        if (Titulo == novoTitulo)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarTitulo", "Título alterado.");
        Titulo = novoTitulo;
        return historico;
    }

    public HistoricoTarefa? AlterarDescricao(string? descricao, Guid usuarioId, DateTime dataUtc)
    {
        ValidarAutorEData(usuarioId, dataUtc);
        var novoConteudo = NormalizarTextoOpcional(descricao);
        if (Descricao == novoConteudo)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarDescricao",
            novoConteudo is null
                ? "Descrição removida da tarefa."
                : "Descrição alterada.");
        Descricao = novoConteudo;
        return historico;
    }

    public HistoricoTarefa? AlterarStatus(StatusTarefa status, Guid usuarioId, DateTime dataUtc)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Status de tarefa inválido.");
        }

        ValidarAutorEData(usuarioId, dataUtc);
        if (Status == status)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarStatus",
            $"Status alterado de {Status} para {status}.");
        Status = status;
        return historico;
    }

    public HistoricoTarefa? AlterarPrioridade(PrioridadeTarefa prioridade, Guid usuarioId, DateTime dataUtc)
    {
        if (!Enum.IsDefined(prioridade))
        {
            throw new ArgumentOutOfRangeException(nameof(prioridade), "Prioridade de tarefa inválida.");
        }

        ValidarAutorEData(usuarioId, dataUtc);
        if (Prioridade == prioridade)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarPrioridade",
            $"Prioridade alterada de {Prioridade} para {prioridade}.");
        Prioridade = prioridade;
        return historico;
    }

    public HistoricoTarefa? AlterarPrazo(DateTime? prazoUtc, Guid usuarioId, DateTime dataUtc)
    {
        if (prazoUtc.HasValue && prazoUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O prazo deve estar em UTC.", nameof(prazoUtc));
        }

        ValidarAutorEData(usuarioId, dataUtc);
        if (PrazoUtc == prazoUtc)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarPrazo",
            prazoUtc is null
                ? "Prazo removido da tarefa."
                : "Prazo alterado.");
        PrazoUtc = prazoUtc;
        return historico;
    }

    public HistoricoTarefa? AlterarResponsavel(Guid? responsavelId, Guid usuarioId, DateTime dataUtc)
    {
        if (responsavelId.HasValue && responsavelId.Value == Guid.Empty)
        {
            throw new ArgumentException("O identificador do responsável não pode ser vazio.", nameof(responsavelId));
        }

        ValidarAutorEData(usuarioId, dataUtc);
        if (ResponsavelId == responsavelId)
        {
            return null;
        }

        var historico = CriarHistorico(usuarioId, dataUtc, "AlterarResponsavel",
            responsavelId is null
                ? "Responsável removido da tarefa."
                : "Responsável alterado.");
        ResponsavelId = responsavelId;
        return historico;
    }

    private static string? NormalizarTextoOpcional(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static void ValidarAutorEData(Guid usuarioId, DateTime dataUtc)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O usuário responsável pela alteração é obrigatório.", nameof(usuarioId));
        }

        if (dataUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A data da alteração deve estar em UTC.", nameof(dataUtc));
        }
    }

    private HistoricoTarefa CriarHistorico(Guid usuarioId, DateTime dataUtc, string acao, string descricao) =>
        new(Guid.NewGuid(), Id, usuarioId, acao, dataUtc, descricao);
}
