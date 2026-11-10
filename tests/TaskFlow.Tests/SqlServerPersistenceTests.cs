using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Data.Repositories;
using Xunit.Abstractions;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerPersistenceTests : IAsyncLifetime
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqlServerTestDatabase _database;
    private readonly ITestOutputHelper _output;

    public SqlServerPersistenceTests(SqlServerTestDatabase database, ITestOutputHelper output)
    {
        _database = database;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _database.ResetAsync();
        _output.WriteLine($"Isolated SQL Server test database: {_database.DatabaseName}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Usuario_PersisteERecuperaDadosNormalizados()
    {
        var usuario = NewUser();
        await using (var context = _database.CreateContext())
        {
            var repository = new UsuarioRepository(context);
            await repository.AddAsync(usuario);
        }

        await using var readContext = _database.CreateContext();
        var readRepository = new UsuarioRepository(readContext);
        var recovered = await readRepository.GetByIdAsync(usuario.Id);

        Assert.NotNull(recovered);
        Assert.Equal(usuario.Email, recovered.Email);
        Assert.Equal(usuario.Nome, recovered.Nome);
        Assert.True(await readRepository.EmailExistsAsync(" ANA@EXAMPLE.COM "));
        Assert.Equal(usuario.Id, (await readRepository.GetByEmailAsync("ANA@EXAMPLE.COM"))?.Id);
    }

    [Fact]
    public async Task Usuario_RejeitaEmailDuplicadoComDiferencaDeMaiusculas()
    {
        await using var context = _database.CreateContext();
        var repository = new UsuarioRepository(context);
        await repository.AddAsync(NewUser());

        var exception = await Assert.ThrowsAsync<PersistenceConstraintException>(() =>
            repository.AddAsync(NewUser(email: "ANA@EXAMPLE.COM")));

        Assert.Equal(PersistenceConstraintKind.Unique, exception.Kind);
        Assert.Equal(1, await context.Usuarios.CountAsync());
    }

    [Fact]
    public async Task Tarefa_PersisteEnumVersaoEDatasUtc()
    {
        var usuario = NewUser();
        var tarefa = NewTask(usuario.Id, status: StatusTarefa.EmAndamento, responsavelId: usuario.Id);
        await using (var context = _database.CreateContext())
        {
            await new UsuarioRepository(context).AddAsync(usuario);
            await new TarefaRepository(context).AddAsync(tarefa);
        }

        await using var readContext = _database.CreateContext();
        var recovered = await new TarefaRepository(readContext).GetByIdAsync(tarefa.Id);

        Assert.NotNull(recovered);
        Assert.Equal(StatusTarefa.EmAndamento, recovered.Status);
        Assert.Equal(PrioridadeTarefa.Media, recovered.Prioridade);
        Assert.Equal(DateTimeKind.Utc, recovered.CriadaEmUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, recovered.PrazoUtc?.Kind);
        Assert.NotNull(recovered.RowVersion);
        Assert.Equal(8, recovered.RowVersion.Length);

        var metadata = readContext.Model.FindEntityType(typeof(Tarefa))!.FindProperty(nameof(Tarefa.RowVersion))!;
        Assert.Equal("Versao", metadata.GetColumnName());
        Assert.True(metadata.IsConcurrencyToken);
        Assert.True(metadata.IsNullable is false);
    }

    [Fact]
    public async Task SqlSchema_AplicaDefaultsDeIdsEnumsEDatasUtc()
    {
        await using var context = _database.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.Usuarios (Nome, Email, PasswordHash, Perfil) VALUES ({"Default User"}, {"defaults@example.com"}, {"opaque-test-hash"}, {(byte)PerfilUsuario.Funcionario})");

        var usuario = await new UsuarioRepository(context).GetByEmailAsync("defaults@example.com");
        Assert.NotNull(usuario);
        Assert.NotEqual(Guid.Empty, usuario.Id);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.Tarefas (Titulo, CriadorId) VALUES ({"Default Task"}, {usuario.Id})");

        var tarefa = await context.Tarefas.SingleAsync(x => x.Titulo == "Default Task");
        Assert.NotEqual(Guid.Empty, tarefa.Id);
        Assert.Equal(StatusTarefa.Pendente, tarefa.Status);
        Assert.Equal(PrioridadeTarefa.Media, tarefa.Prioridade);
        Assert.Equal(DateTimeKind.Utc, tarefa.CriadaEmUtc.Kind);
        Assert.NotNull(tarefa.RowVersion);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.HistoricoTarefas (TarefaId, UsuarioId, Acao) VALUES ({tarefa.Id}, {usuario.Id}, {"Default History"})");

        var historico = await context.HistoricoTarefas.SingleAsync(x => x.TarefaId == tarefa.Id);
        Assert.NotEqual(Guid.Empty, historico.Id);
        Assert.Equal(DateTimeKind.Utc, historico.DataUtc.Kind);
    }

    [Fact]
    public async Task Tarefa_RejeitaCriadorInexistentePorChaveEstrangeira()
    {
        await using var context = _database.CreateContext();

        var exception = await Assert.ThrowsAsync<PersistenceConstraintException>(() =>
            new TarefaRepository(context).AddAsync(NewTask(Guid.NewGuid())));

        Assert.Equal(PersistenceConstraintKind.ForeignKeyOrCheck, exception.Kind);
        Assert.Equal(0, await context.Tarefas.CountAsync());
    }

    [Fact]
    public async Task Historico_PersisteERecuperaEmOrdemUtc()
    {
        var usuario = NewUser();
        var tarefa = NewTask(usuario.Id);
        var historico = new HistoricoTarefa(
            Guid.NewGuid(), tarefa.Id, usuario.Id, "Criar", CreatedAtUtc, "Tarefa criada.");

        await using (var context = _database.CreateContext())
        {
            await new UsuarioRepository(context).AddAsync(usuario);
            await new TarefaRepository(context).AddAsync(tarefa);
            await new HistoricoTarefaRepository(context).AddAsync(historico);
        }

        await using var readContext = _database.CreateContext();
        var recovered = await new HistoricoTarefaRepository(readContext).GetByTarefaIdAsync(tarefa.Id);

        var persisted = Assert.Single(recovered);
        Assert.Equal(historico.Id, persisted.Id);
        Assert.Equal(DateTimeKind.Utc, persisted.DataUtc.Kind);
        Assert.Equal("Criar", persisted.Acao);
    }

    [Fact]
    public async Task Tarefa_AplicaFiltrosEPaginacaoDeterministica()
    {
        var usuario = NewUser();
        await using var context = _database.CreateContext();
        var userRepository = new UsuarioRepository(context);
        var taskRepository = new TarefaRepository(context);
        await userRepository.AddAsync(usuario);

        var tasks = new[]
        {
            NewTask(usuario.Id, "Alpha 1", StatusTarefa.Pendente, usuario.Id, id: Guid.Parse("00000000-0000-0000-0000-000000000001")),
            NewTask(usuario.Id, "Alpha 2", StatusTarefa.Pendente, usuario.Id, id: Guid.Parse("00000000-0000-0000-0000-000000000002")),
            NewTask(usuario.Id, "Alpha 3", StatusTarefa.Pendente, usuario.Id, id: Guid.Parse("00000000-0000-0000-0000-000000000003")),
            NewTask(usuario.Id, "Alpha ignored status", StatusTarefa.Concluida, usuario.Id),
            NewTask(usuario.Id, "Beta ignored title", StatusTarefa.Pendente, usuario.Id)
        };

        foreach (var task in tasks)
        {
            await taskRepository.AddAsync(task);
        }

        var allMatches = await taskRepository.ListAsync(
            0, 20, StatusTarefa.Pendente, usuario.Id, " Alpha ");
        var firstPage = await taskRepository.ListAsync(
            0, 1, StatusTarefa.Pendente, usuario.Id, "Alpha");
        var secondPage = await taskRepository.ListAsync(
            1, 2, StatusTarefa.Pendente, usuario.Id, "Alpha");
        var repeatedPage = await taskRepository.ListAsync(
            0, 1, StatusTarefa.Pendente, usuario.Id, "Alpha");

        Assert.Equal(3, allMatches.Count);
        Assert.Single(firstPage);
        Assert.Equal(allMatches.Take(1).Select(x => x.Id), firstPage.Select(x => x.Id));
        Assert.Equal(allMatches.Skip(1).Select(x => x.Id), secondPage.Select(x => x.Id));
        Assert.Equal(firstPage.Select(x => x.Id), repeatedPage.Select(x => x.Id));
    }

    [Fact]
    public async Task Tarefa_RowVersionRejeitaEscritaConcorrenteSemSobrescrever()
    {
        var usuario = NewUser();
        var tarefa = NewTask(usuario.Id);
        await using (var seedContext = _database.CreateContext())
        {
            await new UsuarioRepository(seedContext).AddAsync(usuario);
            await new TarefaRepository(seedContext).AddAsync(tarefa);
        }

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var firstRepository = new TarefaRepository(firstContext);
        var secondRepository = new TarefaRepository(secondContext);
        var first = Assert.IsType<Tarefa>(await firstRepository.GetByIdAsync(tarefa.Id));
        var second = Assert.IsType<Tarefa>(await secondRepository.GetByIdAsync(tarefa.Id));
        var originalToken = Assert.IsType<byte[]>(first.RowVersion);
        var staleToken = Assert.IsType<byte[]>(second.RowVersion);
        Assert.Equal(originalToken, staleToken);

        var returnedToken = first.RowVersion!;
        returnedToken[0] ^= 0xFF;
        Assert.Equal(originalToken, first.RowVersion);

        var firstHistory = first.AlterarStatus(StatusTarefa.EmAndamento, usuario.Id, CreatedAtUtc);
        Assert.NotNull(firstHistory);
        await firstRepository.UpdateAsync(first);
        Assert.NotEqual(originalToken, first.RowVersion);

        Assert.NotNull(second.AlterarStatus(StatusTarefa.Concluida, usuario.Id, CreatedAtUtc));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondRepository.UpdateAsync(second));

        await using var verificationContext = _database.CreateContext();
        var current = await new TarefaRepository(verificationContext).GetByIdAsync(tarefa.Id);
        Assert.Equal(StatusTarefa.EmAndamento, current?.Status);
        Assert.NotEqual(originalToken, current?.RowVersion);
    }

    [Fact]
    public async Task UnitOfWork_ConfirmaCommitERollbackDeEscritas()
    {
        var committedUser = NewUser();
        var rolledBackUser = NewUser("Rollback user", "rollback@example.com");
        await using (var context = _database.CreateContext())
        {
            var repository = new UsuarioRepository(context);
            var unitOfWork = new TaskFlowUnitOfWork(context);

            await using (var transaction = await unitOfWork.BeginTransactionAsync())
            {
                await repository.AddAsync(committedUser);
                await transaction.CommitAsync();
            }

            await using (var transaction = await unitOfWork.BeginTransactionAsync())
            {
                await repository.AddAsync(rolledBackUser);
                await transaction.RollbackAsync();
            }
        }

        await using var verificationContext = _database.CreateContext();
        var readRepository = new UsuarioRepository(verificationContext);
        Assert.NotNull(await readRepository.GetByIdAsync(committedUser.Id));
        Assert.Null(await readRepository.GetByIdAsync(rolledBackUser.Id));
    }

    [Fact]
    public async Task UpdateAndHistory_CommitTogetherAndSameValueDoesNotDuplicateHistory()
    {
        var usuario = NewUser();
        var tarefa = NewTask(usuario.Id);
        await using (var seedContext = _database.CreateContext())
        {
            await new UsuarioRepository(seedContext).AddAsync(usuario);
            await new TarefaRepository(seedContext).AddAsync(tarefa);
        }

        await using (var context = _database.CreateContext())
        {
            var taskRepository = new TarefaRepository(context);
            var historyRepository = new HistoricoTarefaRepository(context);
            var unitOfWork = new TaskFlowUnitOfWork(context);
            var loaded = Assert.IsType<Tarefa>(await taskRepository.GetByIdAsync(tarefa.Id));
            var history = loaded.AlterarStatus(StatusTarefa.EmAndamento, usuario.Id, CreatedAtUtc);
            Assert.NotNull(history);
            Assert.Null(loaded.AlterarStatus(StatusTarefa.EmAndamento, usuario.Id, CreatedAtUtc));

            await using var transaction = await unitOfWork.BeginTransactionAsync();
            await taskRepository.UpdateAsync(loaded);
            await historyRepository.AddAsync(history);
            await transaction.CommitAsync();
        }

        await using var verificationContext = _database.CreateContext();
        var persistedTask = await new TarefaRepository(verificationContext).GetByIdAsync(tarefa.Id);
        var persistedHistory = await new HistoricoTarefaRepository(verificationContext).GetByTarefaIdAsync(tarefa.Id);
        Assert.Equal(StatusTarefa.EmAndamento, persistedTask?.Status);
        Assert.Single(persistedHistory);
        Assert.Equal("AlterarStatus", persistedHistory[0].Acao);
    }

    [Fact]
    public async Task UpdateAndHistory_RollsBackTaskWhenHistoryViolatesForeignKey()
    {
        var usuario = NewUser();
        var tarefa = NewTask(usuario.Id);
        await using (var seedContext = _database.CreateContext())
        {
            await new UsuarioRepository(seedContext).AddAsync(usuario);
            await new TarefaRepository(seedContext).AddAsync(tarefa);
        }

        await using (var context = _database.CreateContext())
        {
            var taskRepository = new TarefaRepository(context);
            var historyRepository = new HistoricoTarefaRepository(context);
            var unitOfWork = new TaskFlowUnitOfWork(context);
            var loaded = Assert.IsType<Tarefa>(await taskRepository.GetByIdAsync(tarefa.Id));
            var history = Assert.IsType<HistoricoTarefa>(
                loaded.AlterarStatus(StatusTarefa.EmAndamento, usuario.Id, CreatedAtUtc));
            var invalidHistory = new HistoricoTarefa(
                history.Id, history.TarefaId, Guid.NewGuid(), history.Acao, history.DataUtc, history.Descricao);

            await using var transaction = await unitOfWork.BeginTransactionAsync();
            await taskRepository.UpdateAsync(loaded);
            var exception = await Assert.ThrowsAsync<PersistenceConstraintException>(() =>
                historyRepository.AddAsync(invalidHistory));
            Assert.Equal(PersistenceConstraintKind.ForeignKeyOrCheck, exception.Kind);
            await transaction.RollbackAsync();
        }

        await using var verificationContext = _database.CreateContext();
        var persistedTask = await new TarefaRepository(verificationContext).GetByIdAsync(tarefa.Id);
        var persistedHistory = await new HistoricoTarefaRepository(verificationContext).GetByTarefaIdAsync(tarefa.Id);
        Assert.Equal(StatusTarefa.Pendente, persistedTask?.Status);
        Assert.Empty(persistedHistory);
    }

    private static Usuario NewUser(string name = "Ana Silva", string email = "ana@example.com") =>
        new(Guid.NewGuid(), name, email, "test-password-hash", PerfilUsuario.Funcionario);

    private static Tarefa NewTask(
        Guid creatorId,
        string title = "Revisar entrega",
        StatusTarefa status = StatusTarefa.Pendente,
        Guid? responsavelId = null,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            title,
            "Descrição de teste",
            status,
            PrioridadeTarefa.Media,
            CreatedAtUtc,
            CreatedAtUtc.AddDays(1),
            creatorId,
            responsavelId);
}
