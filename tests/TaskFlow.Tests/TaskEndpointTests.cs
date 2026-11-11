using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Data.Repositories;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "Http")]
[Trait("Category", "SqlServer")]
public sealed class TaskEndpointTests
{
    private const string Password = "Task-Creation-Test-Password-42";
    private readonly SqlServerTestDatabase _database;

    public TaskEndpointTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Admin_CreatesTaskWithRealCreatorAssignmentHistoryAndRowVersion()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var employee = await SeedUserAsync("Employee", "employee@task.test", PerfilUsuario.Funcionario);
        var forgedCreatorId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, admin.Email);
        var before = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Prepare release",
            descricao = "Prepare the next internal release.",
            prioridade = PrioridadeTarefa.Alta,
            prazoUtc = DateTime.UtcNow.AddDays(1),
            responsavelId = employee.Id,
            criadorId = forgedCreatorId
        });
        var after = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(created);
        Assert.Equal($"/api/tasks/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Prepare release", created.Titulo);
        Assert.Equal(StatusTarefa.Pendente, created.Status);
        Assert.Equal(PrioridadeTarefa.Alta, created.Prioridade);
        Assert.Equal(admin.Id, created.CriadorId);
        Assert.NotEqual(forgedCreatorId, created.CriadorId);
        Assert.Equal(employee.Id, created.ResponsavelId);
        Assert.InRange(created.CriadaEmUtc, before, after);
        Assert.Equal(DateTimeKind.Utc, created.CriadaEmUtc.Kind);
        Assert.Equal(8, Convert.FromBase64String(created.RowVersion).Length);

        await using var context = _database.CreateContext();
        var persistedTask = await context.Tarefas.SingleAsync(task => task.Id == created.Id);
        Assert.Equal(admin.Id, persistedTask.CriadorId);
        Assert.Equal(employee.Id, persistedTask.ResponsavelId);
        Assert.Equal(StatusTarefa.Pendente, persistedTask.Status);
        Assert.NotNull(persistedTask.RowVersion);
        Assert.Equal(DateTimeKind.Utc, persistedTask.CriadaEmUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, persistedTask.PrazoUtc?.Kind);

        var persistedHistory = await context.HistoricoTarefas
            .Where(item => item.TarefaId == created.Id)
            .ToListAsync();
        var history = Assert.Single(persistedHistory);
        Assert.Equal(admin.Id, history.UsuarioId);
        Assert.Equal("CriarTarefa", history.Acao);
        Assert.Equal("Tarefa criada.", history.Descricao);
        Assert.Equal(DateTimeKind.Utc, history.DataUtc.Kind);
    }

    [Fact]
    public async Task Create_RequiresAuthenticationAndAdministratorRole()
    {
        await _database.ResetAsync();
        await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var employee = await SeedUserAsync("Employee", "employee@task.test", PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        using var anonymousResponse = await PostValidTaskAsync(anonymous);
        using var employeeClient = await LoginAsync(factory, employee.Email);
        using var employeeResponse = await PostValidTaskAsync(employeeClient);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeResponse.StatusCode);

        await using var context = _database.CreateContext();
        Assert.Empty(await context.Tarefas.ToListAsync());
    }

    [Theory]
    [InlineData("{\"descricao\":\"no title\",\"prioridade\":1}", "titulo")]
    [InlineData("{\"titulo\":\"\",\"prioridade\":1}", "titulo")]
    [InlineData("{\"titulo\":\"   \",\"prioridade\":1}", "titulo")]
    [InlineData("{\"titulo\":\"This title is intentionally longer than the allowed two hundred characters. This title is intentionally longer than the allowed two hundred characters. This title is intentionally longer than the allowed two hundred characters.\",\"prioridade\":1}", "titulo")]
    [InlineData("{\"titulo\":\"Valid title\",\"prioridade\":99}", "prioridade")]
    [InlineData("{\"titulo\":\"Valid title\",\"prioridade\":0}", "prioridade")]
    [InlineData("{\"titulo\":\"Valid title\",\"prioridade\":1,\"responsavelId\":\"00000000-0000-0000-0000-000000000000\"}", "responsavelId")]
    [InlineData("{\"titulo\":\"Valid title\",\"prioridade\":1,\"prazoUtc\":\"2026-11-12T12:00:00\"}", "prazoUtc")]
    public async Task Create_InvalidFieldsReturnBadRequestWithValidationErrors(string json, string expectedField)
    {
        await _database.ResetAsync();
        await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, "admin@task.test");
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Errors.ContainsKey(expectedField));
        await using var context = _database.CreateContext();
        Assert.Empty(await context.Tarefas.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsMissingBodyAsBadRequest()
    {
        await _database.ResetAsync();
        await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, "admin@task.test");
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsNotFoundForMissingResponsibleAndAllowsUnassignedTask()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, admin.Email);

        using var missingResponsible = await client.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Invalid assignment",
            prioridade = PrioridadeTarefa.Media,
            responsavelId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.NotFound, missingResponsible.StatusCode);

        using var adminAsResponsible = await client.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Invalid profile assignment",
            prioridade = PrioridadeTarefa.Media,
            responsavelId = admin.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, adminAsResponsible.StatusCode);

        using var unassigned = await client.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Unassigned task",
            prioridade = PrioridadeTarefa.Baixa
        });
        Assert.Equal(HttpStatusCode.Created, unassigned.StatusCode);
        var created = await unassigned.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(created);
        Assert.Null(created.ResponsavelId);

        await using var context = _database.CreateContext();
        Assert.Single(await context.Tarefas.ToListAsync());
        Assert.Single(await context.HistoricoTarefas.ToListAsync());
    }

    [Fact]
    public async Task Create_RollsBackTaskWhenHistoryPersistenceFails()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        await using var factory = CreateFactory(failAfterHistorySave: true);
        using var client = await LoginAsync(factory, admin.Email);

        using var response = await PostValidTaskAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var context = _database.CreateContext();
        Assert.Empty(await context.Tarefas.ToListAsync());
        Assert.Empty(await context.HistoricoTarefas.ToListAsync());
    }

    [Fact]
    public async Task GetById_EnforcesAdminCreatorAndAssigneeVisibility()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var assignee = await SeedUserAsync("Assignee", "assignee@task.test", PerfilUsuario.Funcionario);
        var outsider = await SeedUserAsync("Outsider", "outsider@task.test", PerfilUsuario.Funcionario);
        var task = new Tarefa(
            Guid.NewGuid(),
            "Visible to linked users",
            null,
            StatusTarefa.Pendente,
            PrioridadeTarefa.Media,
            DateTime.UtcNow,
            null,
            creator.Id,
            assignee.Id);
        await using (var context = _database.CreateContext())
        {
            await new TarefaRepository(context).AddAsync(task);
        }

        await using var factory = CreateFactory();
        using var adminClient = await LoginAsync(factory, admin.Email);
        using var creatorClient = await LoginAsync(factory, creator.Email);
        using var assigneeClient = await LoginAsync(factory, assignee.Email);
        using var outsiderClient = await LoginAsync(factory, outsider.Email);

        using var adminResponse = await adminClient.GetAsync($"/api/tasks/{task.Id}");
        using var creatorResponse = await creatorClient.GetAsync($"/api/tasks/{task.Id}");
        using var assigneeResponse = await assigneeClient.GetAsync($"/api/tasks/{task.Id}");
        using var outsiderResponse = await outsiderClient.GetAsync($"/api/tasks/{task.Id}");
        using var notFoundResponse = await adminClient.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creatorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assigneeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
    }

    [Fact]
    public async Task AdminCreatorAndAssignee_CanReadHistoryButAnonymousAndUnlinkedUsersCannot()
    {
        await _database.ResetAsync();
        Assert.StartsWith("TaskFlowIntegrationTests_", _database.DatabaseName);
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var assignee = await SeedUserAsync("Assignee", "assignee@task.test", PerfilUsuario.Funcionario);
        var outsider = await SeedUserAsync("Outsider", "outsider@task.test", PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();

        using var adminClient = await LoginAsync(factory, admin.Email);
        using var createdEmployee = await adminClient.PostAsJsonAsync("/api/users", new
        {
            nome = "New Employee",
            email = "new-employee@task.test",
            senha = Password,
            perfil = PerfilUsuario.Funcionario
        });
        Assert.Equal(HttpStatusCode.Created, createdEmployee.StatusCode);
        var employee = await createdEmployee.Content.ReadFromJsonAsync<PublicUserResponse>();
        Assert.NotNull(employee);

        using var createdTask = await adminClient.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Sprint review task",
            prioridade = PrioridadeTarefa.Media,
            responsavelId = employee.Id
        });
        Assert.Equal(HttpStatusCode.Created, createdTask.StatusCode);
        var task = await createdTask.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(task);
        var creatorTask = await SeedTaskAsync(creator.Id);
        await using (var historyContext = _database.CreateContext())
        {
            await new HistoricoTarefaRepository(historyContext).AddAsync(
                NewHistory(
                    creatorTask.Id,
                    creator.Id,
                    DateTime.UtcNow,
                    "CriarTarefa",
                    Guid.NewGuid().ToString()));
        }

        using var detail = await adminClient.GetAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var adminHistory = await adminClient.GetAsync($"/api/tasks/{task.Id}/history");
        Assert.Equal(HttpStatusCode.OK, adminHistory.StatusCode);
        var adminResult = await adminHistory.Content.ReadFromJsonAsync<PagedTaskHistoryResponse>();
        Assert.NotNull(adminResult);
        Assert.Equal(1, adminResult.TotalCount);
        var creationEvent = Assert.Single(adminResult.Items);
        Assert.Equal(task.Id, creationEvent.TarefaId);
        Assert.Equal(admin.Id, creationEvent.UsuarioId);
        Assert.Equal("CriarTarefa", creationEvent.Acao);
        Assert.Equal("Tarefa criada.", creationEvent.Descricao);
        Assert.Equal(DateTimeKind.Utc, creationEvent.DataUtc.Kind);

        using var creatorClient = await LoginAsync(factory, creator.Email);
        using var assigneeClient = await LoginAsync(factory, employee.Email);
        using var outsiderClient = await LoginAsync(factory, outsider.Email);
        using var anonymousClient = factory.CreateClient();
        using var creatorHistory = await creatorClient.GetAsync($"/api/tasks/{creatorTask.Id}/history");
        using var assigneeDetail = await assigneeClient.GetAsync($"/api/tasks/{task.Id}");
        using var assigneeHistory = await assigneeClient.GetAsync($"/api/tasks/{task.Id}/history");
        using var outsiderHistory = await outsiderClient.GetAsync($"/api/tasks/{task.Id}/history");
        using var anonymousHistory = await anonymousClient.GetAsync($"/api/tasks/{task.Id}/history");
        using var missingTask = await adminClient.GetAsync($"/api/tasks/{Guid.NewGuid()}/history");
        using var emptyId = await adminClient.GetAsync($"/api/tasks/{Guid.Empty}/history");
        using var malformedId = await adminClient.GetAsync("/api/tasks/not-a-guid/history");
        using var attemptedHistoryWrite = await adminClient.PostAsJsonAsync(
            $"/api/tasks/{task.Id}/history",
            new { acao = "Alterar histórico" });
        using var attemptedHistoryDelete = await adminClient.DeleteAsync($"/api/tasks/{task.Id}/history");

        Assert.Equal(HttpStatusCode.OK, creatorHistory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assigneeDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assigneeHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHistory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingTask.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformedId.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, attemptedHistoryWrite.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, attemptedHistoryDelete.StatusCode);

        await using var context = _database.CreateContext();
        var storedTask = await context.Tarefas.SingleAsync(item => item.Id == task.Id);
        Assert.Equal(admin.Id, storedTask.CriadorId);
        Assert.Equal(employee.Id, storedTask.ResponsavelId);
        var storedHistory = await context.HistoricoTarefas
            .Where(item => item.TarefaId == task.Id)
            .ToListAsync();
        var singleEvent = Assert.Single(storedHistory);
        Assert.Equal(admin.Id, singleEvent.UsuarioId);
    }

    [Fact]
    public async Task History_ReturnsEmptyPagedCollectionForExistingTaskWithoutEvents()
    {
        await _database.ResetAsync();
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var task = await SeedTaskAsync(creator.Id);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, creator.Email);

        using var response = await client.GetAsync($"/api/tasks/{task.Id}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedTaskHistoryResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task History_PaginatesInUtcOrderWithIdAsDeterministicTieBreaker()
    {
        await _database.ResetAsync();
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var task = await SeedTaskAsync(creator.Id);
        var sharedUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            NewHistory(task.Id, creator.Id, sharedUtc.AddSeconds(1), "Later", "00000000-0000-0000-0000-000000000004"),
            NewHistory(task.Id, creator.Id, sharedUtc, "Second", "00000000-0000-0000-0000-000000000002"),
            NewHistory(task.Id, creator.Id, sharedUtc, "First", "00000000-0000-0000-0000-000000000001"),
            NewHistory(task.Id, creator.Id, sharedUtc.AddSeconds(1), "Later first", "00000000-0000-0000-0000-000000000003")
        };
        await using (var context = _database.CreateContext())
        {
            var repository = new HistoricoTarefaRepository(context);
            foreach (var entry in entries)
            {
                await repository.AddAsync(entry);
            }
        }

        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, creator.Email);
        using var firstResponse = await client.GetAsync($"/api/tasks/{task.Id}/history?page=1&pageSize=2");
        using var secondResponse = await client.GetAsync($"/api/tasks/{task.Id}/history?page=2&pageSize=2");
        using var invalidPage = await client.GetAsync($"/api/tasks/{task.Id}/history?page=0");
        using var invalidPageSize = await client.GetAsync($"/api/tasks/{task.Id}/history?pageSize=101");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);

        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PagedTaskHistoryResponse>();
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PagedTaskHistoryResponse>();
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(4, firstPage.TotalCount);
        Assert.Equal(4, secondPage.TotalCount);
        Assert.Equal(new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002")
        }, firstPage.Items.Select(item => item.Id));
        Assert.Equal(new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000004")
        }, secondPage.Items.Select(item => item.Id));
        Assert.All(firstPage.Items.Concat(secondPage.Items), item => Assert.Equal(DateTimeKind.Utc, item.DataUtc.Kind));
    }

    [Fact]
    public async Task TaskList_AppliesVisibilityFiltersAndPaginationInSqlServer()
    {
        await _database.ResetAsync();
        Assert.StartsWith("TaskFlowIntegrationTests_", _database.DatabaseName);
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var employee = await SeedUserAsync("Employee", "employee@task.test", PerfilUsuario.Funcionario);
        var otherEmployee = await SeedUserAsync("Other", "other@task.test", PerfilUsuario.Funcionario);
        var sharedCreatedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        await SeedTaskAsync(
            employee.Id, "Alpha shared", StatusTarefa.Pendente, employee.Id, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000001");
        await SeedTaskAsync(
            admin.Id, "Beta assigned", StatusTarefa.EmAndamento, employee.Id, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000002",
            priority: PrioridadeTarefa.Alta);
        await SeedTaskAsync(
            employee.Id, "Gamma authored", StatusTarefa.Concluida, null, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000003");
        await SeedTaskAsync(
            otherEmployee.Id, "Alpha unrelated", StatusTarefa.Pendente, otherEmployee.Id, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000004");
        await SeedTaskAsync(
            admin.Id, "Unassigned admin", StatusTarefa.Pendente, null, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000005");
        await SeedTaskAsync(
            admin.Id, "Alpha other assignee", StatusTarefa.Pendente, otherEmployee.Id, sharedCreatedAt,
            id: "00000000-0000-0000-0000-000000000006");

        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var adminClient = await LoginAsync(factory, admin.Email);
        using var employeeClient = await LoginAsync(factory, employee.Email);
        using var adminAllResponse = await adminClient.GetAsync("/api/tasks");
        using var employeeAllResponse = await employeeClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, adminAllResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, employeeAllResponse.StatusCode);

        var adminAll = await adminAllResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        var employeeAll = await employeeAllResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(adminAll);
        Assert.NotNull(employeeAll);
        Assert.Equal(6, adminAll.TotalCount);
        Assert.Equal(6, adminAll.Items.Count);
        Assert.Equal(1, adminAll.Page);
        Assert.Equal(20, adminAll.PageSize);
        Assert.All(adminAll.Items, item => Assert.Equal(8, Convert.FromBase64String(item.RowVersion).Length));
        Assert.Equal(3, employeeAll.TotalCount);
        Assert.Equal(3, employeeAll.Items.Count);
        Assert.Equal(3, employeeAll.Items.Select(item => item.Id).Distinct().Count());
        Assert.Contains(employeeAll.Items, item => item.Titulo == "Alpha shared");
        Assert.Contains(employeeAll.Items, item => item.Titulo == "Beta assigned");
        Assert.Contains(employeeAll.Items, item => item.Titulo == "Gamma authored");
        Assert.DoesNotContain(employeeAll.Items, item => item.Titulo is "Alpha unrelated" or "Unassigned admin" or "Alpha other assignee");
        Assert.All(adminAll.Items, item => Assert.Equal(DateTimeKind.Utc, item.CriadaEmUtc.Kind));

        using var unassignedResponse = await adminClient.GetAsync("/api/tasks?semResponsavel=true");
        var unassigned = await unassignedResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.Equal(HttpStatusCode.OK, unassignedResponse.StatusCode);
        Assert.NotNull(unassigned);
        Assert.Equal(2, unassigned.TotalCount);
        Assert.All(unassigned.Items, item => Assert.Null(item.ResponsavelId));

        using var employeeUnassignedResponse = await employeeClient.GetAsync("/api/tasks?semResponsavel=true");
        var employeeUnassigned = await employeeUnassignedResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(employeeUnassigned);
        Assert.Equal(1, employeeUnassigned.TotalCount);
        Assert.Equal("Gamma authored", Assert.Single(employeeUnassigned.Items).Titulo);

        using var statusResponse = await employeeClient.GetAsync($"/api/tasks?status={(byte)StatusTarefa.Pendente}");
        var statusPage = await statusResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(statusPage);
        Assert.Equal(1, statusPage.TotalCount);
        Assert.Equal(StatusTarefa.Pendente, Assert.Single(statusPage.Items).Status);

        using var priorityResponse = await employeeClient.GetAsync($"/api/tasks?prioridade={(byte)PrioridadeTarefa.Alta}");
        var priorityPage = await priorityResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(priorityPage);
        Assert.Equal("Beta assigned", Assert.Single(priorityPage.Items).Titulo);

        using var titleResponse = await employeeClient.GetAsync("/api/tasks?titulo=Gamma");
        var titlePage = await titleResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(titlePage);
        Assert.Equal("Gamma authored", Assert.Single(titlePage.Items).Titulo);

        using var combinedResponse = await employeeClient.GetAsync(
            $"/api/tasks?status={(byte)StatusTarefa.EmAndamento}&responsavelId={employee.Id}&titulo=Beta&prioridade={(byte)PrioridadeTarefa.Alta}");
        var combined = await combinedResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(combined);
        Assert.Equal(1, combined.TotalCount);
        Assert.Equal("Beta assigned", Assert.Single(combined.Items).Titulo);

        using var cannotWidenResponse = await employeeClient.GetAsync($"/api/tasks?responsavelId={otherEmployee.Id}");
        var cannotWiden = await cannotWidenResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(cannotWiden);
        Assert.Equal(0, cannotWiden.TotalCount);
        Assert.Empty(cannotWiden.Items);

        using var firstPageResponse = await adminClient.GetAsync("/api/tasks?page=1&pageSize=2");
        using var secondPageResponse = await adminClient.GetAsync("/api/tasks?page=2&pageSize=2");
        using var beyondResponse = await adminClient.GetAsync("/api/tasks?page=20&pageSize=2");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        var beyondPage = await beyondResponse.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.NotNull(beyondPage);
        Assert.Equal(6, firstPage.TotalCount);
        Assert.Equal(6, secondPage.TotalCount);
        Assert.Equal(new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000006"),
            Guid.Parse("00000000-0000-0000-0000-000000000005")
        }, firstPage.Items.Select(item => item.Id));
        Assert.Equal(new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Guid.Parse("00000000-0000-0000-0000-000000000003")
        }, secondPage.Items.Select(item => item.Id));
        Assert.Empty(beyondPage.Items);
        Assert.Equal(6, beyondPage.TotalCount);

        foreach (var invalidQuery in new[]
        {
            "/api/tasks?page=0",
            "/api/tasks?page=1000001",
            "/api/tasks?pageSize=0",
            "/api/tasks?pageSize=101",
            "/api/tasks?status=99",
            "/api/tasks?prioridade=0",
            "/api/tasks?responsavelId=00000000-0000-0000-0000-000000000000",
            "/api/tasks?titulo=" + new string('x', Tarefa.TituloMaxLength + 1),
            $"/api/tasks?responsavelId={otherEmployee.Id}&semResponsavel=true"
        })
        {
            using var invalidResponse = await adminClient.GetAsync(invalidQuery);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }

        await using var context = _database.CreateContext();
        Assert.Equal(6, await context.Tarefas.CountAsync());
        Assert.Equal(_database.DatabaseName, context.Database.GetDbConnection().Database);
    }

    [Fact]
    public async Task TaskList_UsesCurrentDatabaseProfileForVisibility()
    {
        await _database.ResetAsync();
        var employee = await SeedUserAsync("Employee", "employee@task.test", PerfilUsuario.Funcionario);
        var other = await SeedUserAsync("Other", "other@task.test", PerfilUsuario.Funcionario);
        await SeedTaskAsync(other.Id, "Other task", StatusTarefa.Pendente, other.Id, DateTime.UtcNow);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, employee.Email);

        using var beforePromotion = await client.GetAsync("/api/tasks");
        var employeePage = await beforePromotion.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(employeePage);
        Assert.Equal(0, employeePage.TotalCount);

        await using (var context = _database.CreateContext())
        {
            var storedEmployee = await context.Usuarios.SingleAsync(user => user.Id == employee.Id);
            storedEmployee.UpdateProfile(storedEmployee.Nome, storedEmployee.Email, PerfilUsuario.Administrador);
            await context.SaveChangesAsync();
        }

        using var afterPromotion = await client.GetAsync("/api/tasks");
        var adminPage = await afterPromotion.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(adminPage);
        Assert.Equal(1, adminPage.TotalCount);
        Assert.Equal("Other task", Assert.Single(adminPage.Items).Titulo);
    }

    [Fact]
    public async Task TaskStatusUpdate_EnforcesPermissionsAndRejectsStaleVersionWithoutDuplicateHistory()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var assignee = await SeedUserAsync("Assignee", "assignee@task.test", PerfilUsuario.Funcionario);
        var outsider = await SeedUserAsync("Outsider", "outsider@task.test", PerfilUsuario.Funcionario);
        var task = await SeedTaskAsync(creator.Id, assigneeId: assignee.Id);
        var originalVersion = Convert.ToBase64String(task.RowVersion!);
        await using var factory = CreateFactory();
        using var adminClient = await LoginAsync(factory, admin.Email);
        using var creatorClient = await LoginAsync(factory, creator.Email);
        using var assigneeClient = await LoginAsync(factory, assignee.Email);
        using var outsiderClient = await LoginAsync(factory, outsider.Email);
        using var anonymousClient = factory.CreateClient();
        var originalRequest = new { status = StatusTarefa.EmAndamento, rowVersion = originalVersion };

        using var anonymous = await anonymousClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", originalRequest);
        using var creatorOnly = await creatorClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", originalRequest);
        using var outsiderResponse = await outsiderClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", originalRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, creatorOnly.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, outsiderResponse.StatusCode);

        using var adminUpdate = await adminClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", originalRequest);
        Assert.Equal(HttpStatusCode.OK, adminUpdate.StatusCode);
        var adminResult = await adminUpdate.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(adminResult);
        Assert.NotEqual(originalVersion, adminResult.RowVersion);

        using var assigneeUpdate = await assigneeClient.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new { status = StatusTarefa.Concluida, rowVersion = adminResult.RowVersion });
        Assert.Equal(HttpStatusCode.OK, assigneeUpdate.StatusCode);
        var assigneeResult = await assigneeUpdate.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(assigneeResult);
        Assert.NotEqual(adminResult.RowVersion, assigneeResult.RowVersion);

        using var repeated = await assigneeClient.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new { status = StatusTarefa.Concluida, rowVersion = assigneeResult.RowVersion });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using var staleNoOp = await assigneeClient.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new { status = StatusTarefa.Concluida, rowVersion = originalVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleNoOp.StatusCode);

        await using (var adminProfileContext = _database.CreateContext())
        {
            var storedAdmin = await adminProfileContext.Usuarios.SingleAsync(item => item.Id == admin.Id);
            storedAdmin.UpdateProfile(storedAdmin.Nome, storedAdmin.Email, PerfilUsuario.Funcionario);
            await adminProfileContext.SaveChangesAsync();
        }

        using var demotedAdminUpdate = await adminClient.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new { status = StatusTarefa.Pendente, rowVersion = assigneeResult.RowVersion });
        Assert.Equal(HttpStatusCode.Forbidden, demotedAdminUpdate.StatusCode);

        await using var verification = _database.CreateContext();
        var storedTask = await verification.Tarefas.SingleAsync(item => item.Id == task.Id);
        var history = await verification.HistoricoTarefas
            .Where(item => item.TarefaId == task.Id)
            .OrderBy(item => item.DataUtc)
            .ToListAsync();
        Assert.Equal(StatusTarefa.Concluida, storedTask.Status);
        Assert.Equal(assigneeResult.RowVersion, Convert.ToBase64String(storedTask.RowVersion!));
        Assert.Equal(new[] { admin.Id, assignee.Id }, history.Select(item => item.UsuarioId));
        Assert.All(history, item => Assert.Equal("AlterarStatus", item.Acao));
        Assert.Equal(2, history.Count);
    }

    [Theory]
    [InlineData("{\"status\":2}")]
    [InlineData("{\"status\":2,\"rowVersion\":\"not-base64\"}")]
    [InlineData("{\"status\":2,\"rowVersion\":\"AQIDBAUGBw==\"}")]
    [InlineData("{\"status\":2,\"rowVersion\":\"\"}")]
    public async Task TaskStatusUpdate_RejectsMissingOrInvalidRowVersion(string json)
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var task = await SeedTaskAsync(admin.Id);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, admin.Email);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PatchAsync($"/api/tasks/{task.Id}/status", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = _database.CreateContext();
        Assert.Equal(StatusTarefa.Pendente, (await context.Tarefas.SingleAsync(item => item.Id == task.Id)).Status);
        Assert.Empty(await context.HistoricoTarefas.ToListAsync());
    }

    [Theory]
    [InlineData("99")]
    [InlineData("257")]
    [InlineData("\"invalid\"")]
    public async Task TaskStatusUpdate_RejectsInvalidStatus(string statusJson)
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var task = await SeedTaskAsync(admin.Id);
        var rowVersion = Convert.ToBase64String(task.RowVersion!);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, admin.Email);
        using var content = new StringContent(
            $"{{\"status\":{statusJson},\"rowVersion\":\"{rowVersion}\"}}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PatchAsync($"/api/tasks/{task.Id}/status", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = _database.CreateContext();
        Assert.Equal(StatusTarefa.Pendente, (await context.Tarefas.SingleAsync(item => item.Id == task.Id)).Status);
        Assert.Empty(await context.HistoricoTarefas.ToListAsync());
    }

    [Fact]
    public async Task TaskStatusUpdate_UsesSqlServerRowVersionForConcurrentHttpConflict()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var task = await SeedTaskAsync(admin.Id);
        var originalVersion = Convert.ToBase64String(task.RowVersion!);
        await using var factory = CreateFactory(raceBeforeTaskUpdate: true);
        using var client = await LoginAsync(factory, admin.Email);

        using var response = await client.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new { status = StatusTarefa.EmAndamento, rowVersion = originalVersion });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verification = _database.CreateContext();
        var persistedTask = await verification.Tarefas.SingleAsync(item => item.Id == task.Id);
        Assert.Equal(StatusTarefa.Concluida, persistedTask.Status);
        Assert.NotEqual(originalVersion, Convert.ToBase64String(persistedTask.RowVersion!));
        Assert.Empty(await verification.HistoricoTarefas.Where(item => item.TarefaId == task.Id).ToListAsync());
    }

    [Fact]
    public async Task AdminTaskUpdate_ReassignsAndRemovesAssigneeWithAtomicHistoryAndStableIdentity()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var creator = await SeedUserAsync("Creator", "creator@task.test", PerfilUsuario.Funcionario);
        var oldAssignee = await SeedUserAsync("Old Assignee", "old@task.test", PerfilUsuario.Funcionario);
        var newAssignee = await SeedUserAsync("New Assignee", "new@task.test", PerfilUsuario.Funcionario);
        var createdAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var initialDeadline = createdAt.AddDays(1);
        var task = new Tarefa(
            Guid.NewGuid(),
            "Original title",
            "Original description",
            StatusTarefa.Pendente,
            PrioridadeTarefa.Baixa,
            createdAt,
            initialDeadline,
            creator.Id,
            oldAssignee.Id);
        await using (var context = _database.CreateContext())
        {
            await new TarefaRepository(context).AddAsync(task);
        }

        await using var factory = CreateFactory();
        using var adminClient = await LoginAsync(factory, admin.Email);
        using var creatorClient = await LoginAsync(factory, creator.Email);
        using var oldClient = await LoginAsync(factory, oldAssignee.Email);
        using var newClient = await LoginAsync(factory, newAssignee.Email);
        using var employeeEdit = await oldClient.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            titulo = "Forbidden edit",
            rowVersion = Convert.ToBase64String(task.RowVersion!)
        });
        Assert.Equal(HttpStatusCode.Forbidden, employeeEdit.StatusCode);
        using var creatorStatus = await creatorClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new
        {
            status = StatusTarefa.EmAndamento,
            rowVersion = Convert.ToBase64String(task.RowVersion!)
        });
        Assert.Equal(HttpStatusCode.Forbidden, creatorStatus.StatusCode);

        var nextDeadline = createdAt.AddDays(3);
        using var update = await adminClient.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            titulo = "Updated title",
            descricao = "Updated description",
            prioridade = PrioridadeTarefa.Alta,
            prazoUtc = nextDeadline,
            responsavelId = newAssignee.Id,
            criadorId = admin.Id,
            criadaEmUtc = DateTime.UtcNow.AddYears(10),
            rowVersion = Convert.ToBase64String(task.RowVersion!)
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated title", updated.Titulo);
        Assert.Equal("Updated description", updated.Descricao);
        Assert.Equal(PrioridadeTarefa.Alta, updated.Prioridade);
        Assert.Equal(nextDeadline, updated.PrazoUtc);
        Assert.Equal(newAssignee.Id, updated.ResponsavelId);
        Assert.NotEqual(Convert.ToBase64String(task.RowVersion!), updated.RowVersion);
        Assert.Equal(creator.Id, updated.CriadorId);
        Assert.Equal(createdAt, updated.CriadaEmUtc);

        using var unchangedUpdate = await adminClient.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            titulo = updated.Titulo,
            descricao = updated.Descricao,
            prioridade = updated.Prioridade,
            prazoUtc = updated.PrazoUtc,
            responsavelId = updated.ResponsavelId,
            rowVersion = updated.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, unchangedUpdate.StatusCode);
        var unchanged = await unchangedUpdate.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(unchanged);
        Assert.Equal(updated.RowVersion, unchanged.RowVersion);
        await using (var historyContext = _database.CreateContext())
        {
            Assert.Equal(5, await historyContext.HistoricoTarefas.CountAsync(item => item.TarefaId == task.Id));
        }

        using var oldDetail = await oldClient.GetAsync($"/api/tasks/{task.Id}");
        using var creatorDetail = await creatorClient.GetAsync($"/api/tasks/{task.Id}");
        using var newDetail = await newClient.GetAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, oldDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, creatorDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newDetail.StatusCode);
        using var newStatusUpdate = await newClient.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new
        {
            status = StatusTarefa.EmAndamento,
            rowVersion = updated.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, newStatusUpdate.StatusCode);
        var statusUpdated = await newStatusUpdate.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(statusUpdated);

        using var removeAssignee = await adminClient.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            responsavelId = (Guid?)null,
            rowVersion = statusUpdated.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, removeAssignee.StatusCode);
        var unassigned = await removeAssignee.Content.ReadFromJsonAsync<PublicTaskResponse>();
        Assert.NotNull(unassigned);
        Assert.Null(unassigned.ResponsavelId);

        using var formerNewAssigneeDetail = await newClient.GetAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, formerNewAssigneeDetail.StatusCode);
        using var filteredList = await creatorClient.GetAsync("/api/tasks?titulo=Updated");
        var creatorTasks = await filteredList.Content.ReadFromJsonAsync<PagedTasksResponse>();
        Assert.NotNull(creatorTasks);
        Assert.Contains(creatorTasks.Items, item => item.Id == task.Id);
        using var historyResponse = await creatorClient.GetAsync($"/api/tasks/{task.Id}/history");
        var taskHistory = await historyResponse.Content.ReadFromJsonAsync<PagedTaskHistoryResponse>();
        Assert.NotNull(taskHistory);
        Assert.Equal(
            new[] { "AlterarTitulo", "AlterarDescricao", "AlterarPrioridade", "AlterarPrazo", "AlterarResponsavel", "AlterarStatus", "AlterarResponsavel" }.Order(),
            taskHistory.Items.Select(item => item.Acao).Order());
        await using (var historyOrderContext = _database.CreateContext())
        {
            var expectedHistoryOrder = await historyOrderContext.HistoricoTarefas
                .Where(item => item.TarefaId == task.Id)
                .OrderBy(item => item.DataUtc)
                .ThenBy(item => item.Id)
                .Select(item => item.Id)
                .ToListAsync();
            Assert.Equal(expectedHistoryOrder, taskHistory.Items.Select(item => item.Id));
        }
        Assert.Equal(6, taskHistory.Items.Count(item => item.UsuarioId == admin.Id));
        Assert.Single(taskHistory.Items, item => item.UsuarioId == newAssignee.Id);
        Assert.Single(taskHistory.Items, item => item.Acao == "AlterarStatus");
        Assert.All(taskHistory.Items, item => Assert.Equal(DateTimeKind.Utc, item.DataUtc.Kind));

        await using var verification = _database.CreateContext();
        var storedTask = await verification.Tarefas.SingleAsync(item => item.Id == task.Id);
        Assert.Equal(creator.Id, storedTask.CriadorId);
        Assert.Equal(createdAt, storedTask.CriadaEmUtc);
        Assert.Null(storedTask.ResponsavelId);
        Assert.Equal(StatusTarefa.EmAndamento, storedTask.Status);
    }

    [Fact]
    public async Task AdminTaskUpdate_RejectsUnknownOrNonEmployeeAssignee()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var task = await SeedTaskAsync(admin.Id);
        var rowVersion = Convert.ToBase64String(task.RowVersion!);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, admin.Email);

        using var malformedVersion = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            titulo = "Must not be applied",
            rowVersion = "not-base64"
        });
        Assert.Equal(HttpStatusCode.BadRequest, malformedVersion.StatusCode);
        using var localOffsetDeadline = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            prazoUtc = "2026-11-20T12:00:00-03:00",
            rowVersion
        });
        using var unspecifiedDeadline = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            prazoUtc = "2026-11-20T12:00:00",
            rowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, localOffsetDeadline.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unspecifiedDeadline.StatusCode);

        using var missingAssignee = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            responsavelId = Guid.NewGuid(),
            rowVersion
        });
        using var adminAssignee = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            responsavelId = admin.Id,
            rowVersion
        });
        Assert.Equal(HttpStatusCode.NotFound, missingAssignee.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, adminAssignee.StatusCode);

        await using var verification = _database.CreateContext();
        var storedTask = await verification.Tarefas.SingleAsync(item => item.Id == task.Id);
        Assert.Null(storedTask.ResponsavelId);
        Assert.Empty(await verification.HistoricoTarefas.Where(item => item.TarefaId == task.Id).ToListAsync());
    }

    [Fact]
    public async Task AdminTaskUpdate_RollsBackAllFieldsAndHistoryWhenHistoryWriteFails()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Admin", "admin@task.test", PerfilUsuario.Administrador);
        var task = await SeedTaskAsync(admin.Id);
        var originalVersion = Convert.ToBase64String(task.RowVersion!);
        await using var factory = CreateFactory(failAfterHistorySave: true);
        using var client = await LoginAsync(factory, admin.Email);

        using var response = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            titulo = "Must roll back",
            descricao = "This event insert will be forced to fail after save.",
            rowVersion = originalVersion
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verification = _database.CreateContext();
        var persistedTask = await verification.Tarefas.SingleAsync(item => item.Id == task.Id);
        Assert.Equal("Task for history tests", persistedTask.Titulo);
        Assert.Null(persistedTask.Descricao);
        Assert.Equal(originalVersion, Convert.ToBase64String(persistedTask.RowVersion!));
        Assert.Empty(await verification.HistoricoTarefas.Where(item => item.TarefaId == task.Id).ToListAsync());
    }

    private async Task<Usuario> SeedUserAsync(string name, string email, PerfilUsuario profile)
    {
        var user = new Usuario(Guid.NewGuid(), name, email, "temporary", profile);
        user.UpdatePasswordHash(new PasswordHasher<Usuario>().HashPassword(user, Password));
        await using var context = _database.CreateContext();
        context.Usuarios.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Tarefa> SeedTaskAsync(
        Guid creatorId,
        string title = "Task for history tests",
        StatusTarefa status = StatusTarefa.Pendente,
        Guid? assigneeId = null,
        DateTime? createdAtUtc = null,
        string? id = null,
        PrioridadeTarefa priority = PrioridadeTarefa.Media)
    {
        var task = new Tarefa(
            id is null ? Guid.NewGuid() : Guid.Parse(id),
            title,
            null,
            status,
            priority,
            createdAtUtc ?? DateTime.UtcNow,
            null,
            creatorId,
            assigneeId);
        await using var context = _database.CreateContext();
        await new TarefaRepository(context).AddAsync(task);
        return task;
    }

    private static HistoricoTarefa NewHistory(Guid taskId, Guid userId, DateTime dateUtc, string action, string id) =>
        new(Guid.Parse(id), taskId, userId, action, dateUtc, action);

    private static async Task<HttpResponseMessage> PostValidTaskAsync(HttpClient client) =>
        await client.PostAsJsonAsync("/api/tasks", new
        {
            titulo = "Valid task",
            prioridade = PrioridadeTarefa.Media
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(result);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return client;
    }

    private WebApplicationFactory<Program> CreateFactory(
        bool failAfterHistorySave = false,
        bool raceBeforeTaskUpdate = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TaskFlow"] = _database.ConnectionString,
                    ["Authentication:Jwt:Issuer"] = TestJwtConfiguration.Issuer,
                    ["Authentication:Jwt:Audience"] = TestJwtConfiguration.Audience,
                    ["Authentication:Jwt:SigningKey"] = TestJwtConfiguration.SigningKey
                }));
            builder.UseSetting("ConnectionStrings:TaskFlow", _database.ConnectionString);
            builder.UseSetting("Authentication:Jwt:Issuer", TestJwtConfiguration.Issuer);
            builder.UseSetting("Authentication:Jwt:Audience", TestJwtConfiguration.Audience);
            builder.UseSetting("Authentication:Jwt:SigningKey", TestJwtConfiguration.SigningKey);

            if (failAfterHistorySave)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHistoricoTarefaRepository>();
                    services.AddScoped<IHistoricoTarefaRepository>(serviceProvider =>
                        new FailingHistoryRepository(serviceProvider.GetRequiredService<TaskFlowDbContext>()));
                });
            }

            if (raceBeforeTaskUpdate)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<ITarefaRepository>();
                    services.AddScoped<ITarefaRepository>(serviceProvider =>
                        new RacingTaskRepository(
                            new TarefaRepository(serviceProvider.GetRequiredService<TaskFlowDbContext>()),
                            _database.ConnectionString));
                });
            }
        });

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed class FailingHistoryRepository : IHistoricoTarefaRepository
    {
        private readonly HistoricoTarefaRepository _inner;

        public FailingHistoryRepository(TaskFlowDbContext context)
        {
            _inner = new HistoricoTarefaRepository(context);
        }

        public async Task AddAsync(HistoricoTarefa historico, CancellationToken cancellationToken = default)
        {
            await _inner.AddAsync(historico, cancellationToken);
            throw new InvalidOperationException("Injected history persistence failure.");
        }

        public Task<IReadOnlyList<HistoricoTarefa>> GetByTarefaIdAsync(
            Guid tarefaId,
            CancellationToken cancellationToken = default) =>
            _inner.GetByTarefaIdAsync(tarefaId, cancellationToken);

        public Task<(IReadOnlyList<HistoricoTarefa> History, int TotalCount)> ListPageByTarefaIdAsync(
            Guid tarefaId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            _inner.ListPageByTarefaIdAsync(tarefaId, page, pageSize, cancellationToken);
    }

    private sealed class RacingTaskRepository : ITarefaRepository
    {
        private readonly ITarefaRepository _inner;
        private readonly string _connectionString;
        private int _raced;

        public RacingTaskRepository(ITarefaRepository inner, string connectionString)
        {
            _inner = inner;
            _connectionString = connectionString;
        }

        public Task AddAsync(Tarefa tarefa, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(tarefa, cancellationToken);

        public Task<Tarefa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _inner.GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Tarefa> Tasks, int TotalCount)> ListPageAsync(
            int page,
            int pageSize,
            Guid? visibleUserId,
            StatusTarefa? status = null,
            PrioridadeTarefa? prioridade = null,
            Guid? responsavelId = null,
            bool semResponsavel = false,
            string? titulo = null,
            CancellationToken cancellationToken = default) =>
            _inner.ListPageAsync(
                page,
                pageSize,
                visibleUserId,
                status,
                prioridade,
                responsavelId,
                semResponsavel,
                titulo,
                cancellationToken);

        public Task<IReadOnlyList<Tarefa>> ListAsync(
            int skip,
            int take,
            StatusTarefa? status = null,
            Guid? responsavelId = null,
            string? titulo = null,
            CancellationToken cancellationToken = default) =>
            _inner.ListAsync(skip, take, status, responsavelId, titulo, cancellationToken);

        public async Task UpdateAsync(Tarefa tarefa, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _raced, 1) == 0)
            {
                var options = new DbContextOptionsBuilder<TaskFlowDbContext>()
                    .UseSqlServer(_connectionString)
                    .Options;
                await using var competingContext = new TaskFlowDbContext(options);
                var competingRepository = new TarefaRepository(competingContext);
                var competingTask = await competingRepository.GetByIdAsync(tarefa.Id, cancellationToken);
                Assert.NotNull(competingTask);
                Assert.NotNull(competingTask.AlterarStatus(
                    StatusTarefa.Concluida,
                    competingTask.CriadorId,
                    DateTime.UtcNow));
                await competingRepository.UpdateAsync(competingTask, cancellationToken);
            }

            await _inner.UpdateAsync(tarefa, cancellationToken);
        }
    }
}
