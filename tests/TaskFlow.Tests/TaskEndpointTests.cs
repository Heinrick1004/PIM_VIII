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

    private async Task<Usuario> SeedUserAsync(string name, string email, PerfilUsuario profile)
    {
        var user = new Usuario(Guid.NewGuid(), name, email, "temporary", profile);
        user.UpdatePasswordHash(new PasswordHasher<Usuario>().HashPassword(user, Password));
        await using var context = _database.CreateContext();
        context.Usuarios.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

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

    private WebApplicationFactory<Program> CreateFactory(bool failAfterHistorySave = false) =>
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
    }
}
