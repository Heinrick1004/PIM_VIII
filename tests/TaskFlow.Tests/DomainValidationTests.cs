using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Tests;

[Trait("Category", "Domain")]
public sealed class DomainValidationTests
{
    // Valor sintético opaco; estes testes não implementam um hasher nem autenticação.
    private const string HashDeTeste = "hash-previamente-calculado-para-testes-de-dominio";
    private static readonly DateTime DataUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Usuario_AceitaEmailValidoENomeComEspacosExternos()
    {
        var usuario = new Usuario(Guid.NewGuid(), " Ana Silva ", " ANA@EXAMPLE.COM ", HashDeTeste, PerfilUsuario.Funcionario);

        Assert.Equal("Ana Silva", usuario.Nome);
        Assert.Equal("ana@example.com", usuario.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Usuario_RejeitaEmailVazio(string? email)
    {
        Assert.Throws<ArgumentException>(() =>
            new Usuario(Guid.NewGuid(), "Ana Silva", email!, HashDeTeste, PerfilUsuario.Funcionario));
    }

    [Fact]
    public void Usuario_RejeitaEmailInvalido()
    {
        Assert.Throws<ArgumentException>(() =>
            new Usuario(Guid.NewGuid(), "Ana Silva", "email-invalido", HashDeTeste, PerfilUsuario.Funcionario));
    }

    [Fact]
    public void Tarefa_RejeitaTituloVazio()
    {
        Assert.Throws<ArgumentException>(() =>
            NovaTarefa(" "));
    }

    [Fact]
    public void Tarefa_RejeitaTituloMaiorQueLimite()
    {
        Assert.Throws<ArgumentException>(() =>
            NovaTarefa(new string('a', Tarefa.TituloMaxLength + 1)));
    }

    [Fact]
    public void Tarefa_RejeitaStatusForaDoDominio()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tarefa(Guid.NewGuid(), "Validar entrega", null, (StatusTarefa)99, PrioridadeTarefa.Media,
                DateTime.UtcNow, null, Guid.NewGuid(), null));
    }

    [Fact]
    public void Tarefa_RejeitaDataDeCriacaoQueNaoEstaEmUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            new Tarefa(Guid.NewGuid(), "Validar entrega", null, StatusTarefa.Pendente, PrioridadeTarefa.Media,
                DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local), null, Guid.NewGuid(), null));
    }

    [Fact]
    public void Historico_RejeitaDataQueNaoEstaEmUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            new HistoricoTarefa(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Status alterado",
                DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local), null));
    }

    [Fact]
    public void Usuario_ValidaNomeHashIdEPerfil()
    {
        Assert.Throws<ArgumentException>(() => NovoUsuario(nome: " "));
        Assert.Throws<ArgumentException>(() => NovoUsuario(nome: new string('a', Usuario.NomeMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => NovoUsuario(hash: " "));
        Assert.Throws<ArgumentException>(() => NovoUsuario(hash: new string('a', Usuario.PasswordHashMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => NovoUsuario(email: new string('a', Usuario.EmailMaxLength) + "@example.com"));
        Assert.Throws<ArgumentException>(() =>
            new Usuario(Guid.Empty, "Ana Silva", "ana@example.com", HashDeTeste, PerfilUsuario.Funcionario));
        Assert.Throws<ArgumentOutOfRangeException>(() => NovoUsuario(perfil: (PerfilUsuario)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NovoUsuario(perfil: (PerfilUsuario)3));

        Assert.Equal(Usuario.NomeMaxLength, NovoUsuario(nome: new string('a', Usuario.NomeMaxLength)).Nome.Length);
        Assert.Equal(Usuario.PasswordHashMaxLength, NovoUsuario(hash: new string('a', Usuario.PasswordHashMaxLength)).PasswordHash.Length);
    }

    [Fact]
    public void Tarefa_AceitaLimiteDoTituloEOpcionaisNulos()
    {
        var tarefa = NovaTarefa(" " + new string('a', Tarefa.TituloMaxLength) + " ");

        Assert.Equal(Tarefa.TituloMaxLength, tarefa.Titulo.Length);
        Assert.Null(tarefa.Descricao);
        Assert.Null(tarefa.PrazoUtc);
        Assert.Null(tarefa.ResponsavelId);
        Assert.Null(tarefa.RowVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Tarefa_RejeitaPrioridadeInvalida(int prioridade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NovaTarefa(prioridade: (PrioridadeTarefa)prioridade));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Tarefa_RejeitaCriacaoOuPrazoSemUtc(DateTimeKind kind)
    {
        var dataInvalida = DateTime.SpecifyKind(DataUtc, kind);

        Assert.Throws<ArgumentException>(() => NovaTarefa(criadaEmUtc: dataInvalida));
        Assert.Throws<ArgumentException>(() => NovaTarefa(prazoUtc: dataInvalida));
    }

    [Fact]
    public void Tarefa_RejeitaIdentificadoresVazios()
    {
        Assert.Throws<ArgumentException>(() => NovaTarefa(id: Guid.Empty));
        Assert.Throws<ArgumentException>(() => NovaTarefa(criadorId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => NovaTarefa(responsavelId: Guid.Empty));
    }

    [Fact]
    public void Tarefa_PreservaRowVersionContraAlteracoesExternas()
    {
        byte[] token = [1, 2, 3, 4, 5, 6, 7, 8];
        var tarefa = NovaTarefa(rowVersion: token);
        token[0] = 99;
        var copia = Assert.IsType<byte[]>(tarefa.RowVersion);
        copia[1] = 99;

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, tarefa.RowVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    public void Tarefa_RejeitaRowVersionComTamanhoInvalido(int tamanho)
    {
        Assert.Throws<ArgumentException>(() => NovaTarefa(rowVersion: new byte[tamanho]));
    }

    [Fact]
    public void Tarefa_AlteracoesGeramHistoricoComAutorDataEValores()
    {
        var tarefa = NovaTarefa();
        var usuarioId = Guid.NewGuid();

        var titulo = Assert.IsType<HistoricoTarefa>(tarefa.AlterarTitulo(" Revisar entrega ", usuarioId, DataUtc));
        var status = Assert.IsType<HistoricoTarefa>(tarefa.AlterarStatus(StatusTarefa.EmAndamento, usuarioId, DataUtc));
        var prioridade = Assert.IsType<HistoricoTarefa>(tarefa.AlterarPrioridade(PrioridadeTarefa.Alta, usuarioId, DataUtc));

        Assert.Equal("Revisar entrega", tarefa.Titulo);
        Assert.Equal(StatusTarefa.EmAndamento, tarefa.Status);
        Assert.Equal(PrioridadeTarefa.Alta, tarefa.Prioridade);
        Assert.Equal("AlterarTitulo", titulo.Acao);
        Assert.Equal("AlterarStatus", status.Acao);
        Assert.Equal("AlterarPrioridade", prioridade.Acao);
        Assert.Equal("Título alterado.", titulo.Descricao);
        Assert.Contains("Pendente", status.Descricao);
        Assert.Contains("EmAndamento", status.Descricao);
        Assert.Contains("Media", prioridade.Descricao);
        Assert.Contains("Alta", prioridade.Descricao);
        foreach (var historico in new[] { titulo, status, prioridade })
        {
            Assert.NotEqual(Guid.Empty, historico.Id);
            Assert.Equal(tarefa.Id, historico.TarefaId);
            Assert.Equal(usuarioId, historico.UsuarioId);
            Assert.Equal(DataUtc, historico.DataUtc);
            Assert.Equal(DateTimeKind.Utc, historico.DataUtc.Kind);
        }
    }

    [Fact]
    public void Tarefa_AlteracoesAdministrativasGeramHistoricoSemExporConteudoLivre()
    {
        var tarefa = NovaTarefa();
        var usuarioId = Guid.NewGuid();
        var newDeadline = DataUtc.AddDays(2);
        var newAssignee = Guid.NewGuid();

        var description = Assert.IsType<HistoricoTarefa>(
            tarefa.AlterarDescricao("Private text that must not be copied to the audit log.", usuarioId, DataUtc));
        var deadline = Assert.IsType<HistoricoTarefa>(
            tarefa.AlterarPrazo(newDeadline, usuarioId, DataUtc));
        var assignee = Assert.IsType<HistoricoTarefa>(
            tarefa.AlterarResponsavel(newAssignee, usuarioId, DataUtc));

        Assert.Equal("Private text that must not be copied to the audit log.", tarefa.Descricao);
        Assert.Equal(newDeadline, tarefa.PrazoUtc);
        Assert.Equal(newAssignee, tarefa.ResponsavelId);
        Assert.Equal("AlterarDescricao", description.Acao);
        Assert.Equal("Descrição alterada.", description.Descricao);
        Assert.DoesNotContain("Private text", description.Descricao);
        Assert.Equal("AlterarPrazo", deadline.Acao);
        Assert.Equal("Prazo alterado.", deadline.Descricao);
        Assert.Equal("AlterarResponsavel", assignee.Acao);
        Assert.Equal("Responsável alterado.", assignee.Descricao);

        Assert.Null(tarefa.AlterarDescricao(tarefa.Descricao, usuarioId, DataUtc));
        Assert.Null(tarefa.AlterarPrazo(newDeadline, usuarioId, DataUtc));
        Assert.Null(tarefa.AlterarResponsavel(newAssignee, usuarioId, DataUtc));
        Assert.Equal("Descrição removida da tarefa.",
            tarefa.AlterarDescricao(null, usuarioId, DataUtc)?.Descricao);
        Assert.Equal("Prazo removido da tarefa.",
            tarefa.AlterarPrazo(null, usuarioId, DataUtc)?.Descricao);
        Assert.Equal("Responsável removido da tarefa.",
            tarefa.AlterarResponsavel(null, usuarioId, DataUtc)?.Descricao);
    }

    [Fact]
    public void Tarefa_SetRowVersionRejeitaVersaoInvalidaSemAlterarToken()
    {
        var tarefa = NovaTarefa(rowVersion: new byte[8]);
        var current = tarefa.RowVersion;

        Assert.Throws<ArgumentException>(() => tarefa.SetRowVersion(new byte[7]));
        Assert.Equal(current, tarefa.RowVersion);
    }

    [Fact]
    public void Tarefa_SemMudancaNaoGeraHistorico()
    {
        var tarefa = NovaTarefa();
        var usuarioId = Guid.NewGuid();

        Assert.Null(tarefa.AlterarTitulo(" Validar entrega ", usuarioId, DataUtc));
        Assert.Null(tarefa.AlterarStatus(StatusTarefa.Pendente, usuarioId, DataUtc));
        Assert.Null(tarefa.AlterarPrioridade(PrioridadeTarefa.Media, usuarioId, DataUtc));
    }

    [Fact]
    public void Tarefa_AlteracaoInvalidaPreservaEstado()
    {
        var tarefa = NovaTarefa();
        var usuarioId = Guid.NewGuid();
        var dataInvalida = DateTime.SpecifyKind(DataUtc, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() => tarefa.AlterarTitulo(" ", usuarioId, DataUtc));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarTitulo(new string('a', Tarefa.TituloMaxLength + 1), usuarioId, DataUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => tarefa.AlterarStatus((StatusTarefa)99, usuarioId, DataUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => tarefa.AlterarPrioridade((PrioridadeTarefa)99, usuarioId, DataUtc));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarTitulo("Outro", Guid.Empty, DataUtc));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarStatus(StatusTarefa.Concluida, Guid.Empty, DataUtc));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarPrioridade(PrioridadeTarefa.Alta, Guid.Empty, DataUtc));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarTitulo("Outro", usuarioId, dataInvalida));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarStatus(StatusTarefa.Concluida, usuarioId, dataInvalida));
        Assert.Throws<ArgumentException>(() => tarefa.AlterarPrioridade(PrioridadeTarefa.Alta, usuarioId, dataInvalida));
        Assert.Equal("Validar entrega", tarefa.Titulo);
        Assert.Equal(StatusTarefa.Pendente, tarefa.Status);
        Assert.Equal(PrioridadeTarefa.Media, tarefa.Prioridade);
    }

    [Fact]
    public void Historico_ValidaIdsAcaoEDescricao()
    {
        Assert.Throws<ArgumentException>(() => NovoHistorico(id: Guid.Empty));
        Assert.Throws<ArgumentException>(() => NovoHistorico(tarefaId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => NovoHistorico(usuarioId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => NovoHistorico(acao: " "));
        Assert.Throws<ArgumentException>(() => NovoHistorico(acao: new string('a', HistoricoTarefa.AcaoMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => NovoHistorico(descricao: new string('d', HistoricoTarefa.DescricaoMaxLength + 1)));

        var historico = NovoHistorico(acao: new string('a', HistoricoTarefa.AcaoMaxLength),
            descricao: new string('d', HistoricoTarefa.DescricaoMaxLength));
        Assert.Equal(HistoricoTarefa.AcaoMaxLength, historico.Acao.Length);
        Assert.Equal(HistoricoTarefa.DescricaoMaxLength, historico.Descricao!.Length);
    }

    private static Usuario NovoUsuario(string nome = "Ana Silva", string email = "ana@example.com",
        string hash = HashDeTeste, PerfilUsuario perfil = PerfilUsuario.Funcionario) =>
        new(Guid.NewGuid(), nome, email, hash, perfil);

    private static Tarefa NovaTarefa(string titulo = "Validar entrega", PrioridadeTarefa prioridade = PrioridadeTarefa.Media,
        DateTime? criadaEmUtc = null, DateTime? prazoUtc = null, Guid? id = null, Guid? criadorId = null,
        Guid? responsavelId = null, byte[]? rowVersion = null) =>
        new(id ?? Guid.NewGuid(), titulo, null, StatusTarefa.Pendente, prioridade, criadaEmUtc ?? DataUtc, prazoUtc,
            criadorId ?? Guid.NewGuid(), responsavelId, rowVersion);

    private static HistoricoTarefa NovoHistorico(Guid? id = null, Guid? tarefaId = null, Guid? usuarioId = null,
        string acao = "AlterarStatus", string? descricao = null) =>
        new(id ?? Guid.NewGuid(), tarefaId ?? Guid.NewGuid(), usuarioId ?? Guid.NewGuid(), acao, DataUtc, descricao);
}
