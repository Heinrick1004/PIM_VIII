using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskFlow.Web.Contracts;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

[Trait("Category", "Web")]
public sealed class WebApplicationTests
{
    [Fact]
    public async Task LoginPage_IsAnonymousAndIncludesLocalAssetsWithoutJwt()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);

        using var response = await client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TaskFlow", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.DoesNotContain("test-token-secret", html);
        Assert.Contains("/css/site.css", html);
        Assert.Contains("/js/login.js", html);

        using var css = await client.GetAsync("/css/site.css");
        using var script = await client.GetAsync("/js/login.js");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        var stylesheet = await css.Content.ReadAsStringAsync();
        Assert.Contains("--bg: #0b1020", stylesheet);
        Assert.Contains("--surface-elevated: #1c2740", stylesheet);
        Assert.Contains("--blue: #6478ff", stylesheet);
        Assert.Contains("prefers-reduced-motion: reduce", stylesheet);
    }

    [Fact]
    public async Task AdministrativeFormsAndTaskFiltersExposeRealLoadingStates()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var taskForm = await client.GetAsync("/Tasks/Create");
        using var userForm = await client.GetAsync("/Users/Create");
        using var filters = await client.GetAsync("/Tasks");
        var taskHtml = await taskForm.Content.ReadAsStringAsync();
        var userHtml = await userForm.Content.ReadAsStringAsync();
        var filterHtml = await filters.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, taskForm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, userForm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, filters.StatusCode);
        Assert.Contains("data-loading-form", taskHtml);
        Assert.Contains("data-loading-button", taskHtml);
        Assert.Contains("data-loading-form", userHtml);
        Assert.Contains("data-loading-button", userHtml);
        Assert.Contains("data-loading-form", filterHtml);
        Assert.Contains("data-loading-button", filterHtml);
    }

    [Fact]
    public void ProductionConfiguration_RejectsHttpApiBaseUrl()
    {
        using var factory = new WebTestFactory("Production", "http://localhost:5046");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("HTTPS", exception.Message);
    }

    [Fact]
    public async Task ProtectedDashboard_RedirectsAnonymousVisitorToLogin()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Login_WithRealApiContractStoresProtectedCookieAndUsesApiDashboardCounts()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);

        using var loginResponse = await LoginAsync(client, "admin@taskflow.test");
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Contains(loginResponse.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("TaskFlow.Web.Auth=", StringComparison.Ordinal)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        using var dashboardResponse = await client.GetAsync("/");
        var html = await dashboardResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Admin User", html);
        Assert.Contains(">16<", html);
        Assert.Contains(">8<", html);
        Assert.Contains("Tarefa real de teste", html);
        Assert.DoesNotContain("test-token-secret", html);
        Assert.Equal("test-token-secret", factory.Api.LastAuthorizationToken);
    }

    [Fact]
    public async Task InvalidLogin_ShowsCredentialErrorWithoutCookie()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");

        using var response = await client.PostAsync("/Account/Login", Form(
            ("Email", "invalid@taskflow.test"),
            ("Password", "wrong"),
            ("__RequestVerificationToken", token)));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("E-mail ou senha incorretos.", html);
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("TaskFlow.Web.Auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_PostRequiresAntiforgeryAndEndsSession()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, "/");

        using var rejectedPost = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]));
        using var logout = await client.PostAsync("/Account/Logout", Form(
            ("__RequestVerificationToken", token)));
        using var afterLogout = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedPost.StatusCode);
    }

    [Fact]
    public async Task Dashboard_DisplaysApiUnavailableWithoutInventingCounts()
    {
        using var factory = new WebTestFactory();
        factory.Api.TaskListStatusCode = StatusCodes.Status503ServiceUnavailable;
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plainText = PlainText(html);
        Assert.Contains("A API está indisponível para o teste.", plainText);
        Assert.DoesNotContain("Tarefas visíveis", plainText);
    }

    [Fact]
    public async Task TaskList_PassesFiltersAndPaginationToApi()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var response = await client.GetAsync(
            "/Tasks?page=2&pageSize=10&status=2&prioridade=3&semResponsavel=true&titulo=vers%C3%A3o");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tarefa real de teste", html);
        Assert.Equal(2, factory.Api.LastTaskPage);
        Assert.Equal(10, factory.Api.LastTaskPageSize);
        Assert.Equal(StatusTarefa.EmAndamento, factory.Api.LastTaskStatus);
        Assert.Equal(PrioridadeTarefa.Alta, factory.Api.LastTaskPriority);
        Assert.True(factory.Api.LastWithoutResponsible);
        Assert.Equal("versão", factory.Api.LastTaskTitle);
        var linkMarkup = html.Replace("&amp;", "&", StringComparison.Ordinal);
        Assert.Contains("page=1", linkMarkup);
        Assert.Contains("status=EmAndamento", linkMarkup);
        Assert.Contains("prioridade=Alta", linkMarkup);
        Assert.Contains("semResponsavel=True", linkMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("titulo=vers%C3%A3o", linkMarkup);
    }

    [Fact]
    public async Task Employee_CannotOpenUserManagementOrAdministrativeTaskForms()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "employee@taskflow.test");

        using var users = await client.GetAsync("/Users");
        using var createTask = await client.GetAsync("/Tasks/Create");
        using var editTask = await client.GetAsync($"/Tasks/Edit/{FakeTaskFlowApiClient.TaskId}");

        Assert.Equal(HttpStatusCode.Redirect, users.StatusCode);
        Assert.Equal("/Account/AccessDenied", users.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.Redirect, createTask.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, editTask.StatusCode);
        Assert.DoesNotContain("/Users", users.Headers.Location?.AbsolutePath ?? "");
    }

    [Fact]
    public async Task AdministratorCanOpenUserManagementAfterProfileValidation()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Api.CurrentUserCallCount);
        Assert.Equal(1, factory.Api.UsersListCallCount);
    }

    [Fact]
    public async Task TaskCreation_SendsUtcDeadlineAndSelectedAssignee()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, "/Tasks/Create");

        using var response = await client.PostAsync("/Tasks/Create", Form(
            ("Title", "Nova entrega"),
            ("Description", ""),
            ("Priority", "3"),
            ("DueLocal", "2026-11-12T10:30"),
            ("TimezoneOffsetMinutes", "180"),
            ("ResponsibleId", FakeTaskFlowApiClient.EmployeeId.ToString()),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Tasks/Details/{FakeTaskFlowApiClient.TaskId}", LocationPath(response));
        using var request = JsonDocument.Parse(factory.Api.LastCreateTaskRequest!);
        Assert.Equal("2026-11-12T13:30:00Z", request.RootElement.GetProperty("prazoUtc").GetDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        Assert.Equal(FakeTaskFlowApiClient.EmployeeId, request.RootElement.GetProperty("responsavelId").GetGuid());
    }

    [Fact]
    public async Task TaskEdit_SendsRowVersionAndNullsToRemoveDescriptionDeadlineAndAssignee()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, $"/Tasks/Edit/{FakeTaskFlowApiClient.TaskId}");

        using var response = await client.PostAsync(
            $"/Tasks/Edit/{FakeTaskFlowApiClient.TaskId}",
            Form(
                ("Title", "Título atualizado"),
                ("Description", ""),
                ("Priority", "2"),
                ("DueLocal", ""),
                ("ResponsibleId", ""),
                ("RowVersion", FakeTaskFlowApiClient.RowVersion),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var request = JsonDocument.Parse(factory.Api.LastUpdateTaskRequest!);
        Assert.Equal("Título atualizado", request.RootElement.GetProperty("titulo").GetString());
        Assert.Equal(JsonValueKind.Null, request.RootElement.GetProperty("descricao").ValueKind);
        Assert.Equal(JsonValueKind.Null, request.RootElement.GetProperty("prazoUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, request.RootElement.GetProperty("responsavelId").ValueKind);
        Assert.Equal(FakeTaskFlowApiClient.RowVersion, request.RootElement.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task UserEdit_UsesRealUserApiWithoutSubmittingPassword()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, $"/Users/Edit/{FakeTaskFlowApiClient.EmployeeId}");

        using var response = await client.PostAsync(
            $"/Users/Edit/{FakeTaskFlowApiClient.EmployeeId}",
            Form(
                ("Name", "Nome corrigido"),
                ("Email", "corrected@taskflow.test"),
                ("Profile", "2"),
                ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var request = JsonDocument.Parse(factory.Api.LastUpdateUserRequest!);
        Assert.Equal("Nome corrigido", request.RootElement.GetProperty("nome").GetString());
        Assert.Equal("corrected@taskflow.test", request.RootElement.GetProperty("email").GetString());
        Assert.Equal(2, request.RootElement.GetProperty("perfil").GetInt32());
        Assert.False(request.RootElement.TryGetProperty("senha", out _));
    }

    [Fact]
    public async Task CreatorOnlyEmployee_CanViewTaskButDoesNotReceiveStatusForm()
    {
        using var factory = new WebTestFactory();
        factory.Api.TaskData = new PublicTask(
            FakeTaskFlowApiClient.TaskId,
            "Tarefa criada pelo funcionário",
            null,
            StatusTarefa.Pendente,
            PrioridadeTarefa.Media,
            DateTime.UtcNow,
            null,
            FakeTaskFlowApiClient.EmployeeId,
            Guid.NewGuid(),
            FakeTaskFlowApiClient.RowVersion);
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "employee@taskflow.test");

        using var response = await client.GetAsync($"/Tasks/Details/{FakeTaskFlowApiClient.TaskId}");
        var html = PlainText(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tarefa criada pelo funcionário", html);
        Assert.DoesNotContain("Atualizar status", html);
    }

    [Fact]
    public async Task EmployeeCannotPostAdministrativeTaskCreation()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "employee@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, "/Tasks");

        using var response = await client.PostAsync("/Tasks/Create", Form(
            ("Title", "Denied"),
            ("__RequestVerificationToken", token)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
        Assert.Null(factory.Api.LastCreateTaskRequest);
    }

    [Fact]
    public async Task StatusChange_SendsCurrentRowVersionAnd409IsShownAsNotSaved()
    {
        using var factory = new WebTestFactory();
        factory.Api.StatusUpdateCode = StatusCodes.Status409Conflict;
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "employee@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, $"/Tasks/Details/{FakeTaskFlowApiClient.TaskId}");

        using var response = await client.PostAsync(
            $"/Tasks/ChangeStatus/{FakeTaskFlowApiClient.TaskId}",
            Form(
                ("status", "2"),
                ("rowVersion", FakeTaskFlowApiClient.RowVersion),
                ("__RequestVerificationToken", token)));
        using var detailResponse = await client.GetAsync($"/Tasks/Details/{FakeTaskFlowApiClient.TaskId}");
        var html = await detailResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(StatusTarefa.EmAndamento, factory.Api.LastRequestedStatus);
        Assert.Equal(FakeTaskFlowApiClient.RowVersion, factory.Api.LastRequestedRowVersion);
        var plainText = PlainText(html);
        Assert.Contains("O status não foi alterado", plainText);
        Assert.DoesNotContain("Status atualizado.", plainText);
    }

    [Fact]
    public async Task FormPosts_WithoutAntiforgeryTokenAreRejected()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var response = await client.PostAsync("/Tasks/Create", Form(("Title", "Sem token")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.Api.LastCreateTaskRequest);
    }

    [Fact]
    public async Task UserManagement_UsesApiAndReportsConflictsWithoutFalseSuccess()
    {
        using var factory = new WebTestFactory();
        factory.Api.UserCreateCode = StatusCodes.Status409Conflict;
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var token = await GetAntiforgeryTokenAsync(client, "/Users/Create");

        using var response = await client.PostAsync("/Users/Create", Form(
            ("Name", "Novo funcionário"),
            ("Email", "new@taskflow.test"),
            ("Password", "strong-password-123"),
            ("Profile", "2"),
            ("__RequestVerificationToken", token)));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plainText = PlainText(html);
        Assert.Contains("E-mail já cadastrado.", plainText);
        Assert.DoesNotContain("Usuário cadastrado.", plainText);
    }

    [Fact]
    public async Task DemotedAdmin_IsRejectedOnNextRequestEvenWhenCookieStillSaysAdmin()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        var antiforgery = await GetAntiforgeryTokenAsync(client, "/Users/Create");
        factory.Api.CurrentUser = new PublicUser(
            FakeTaskFlowApiClient.AdminId,
            "Admin User",
            "admin@taskflow.test",
            PerfilUsuario.Funcionario);

        using var response = await client.GetAsync("/Users");
        using var createResponse = await client.PostAsync("/Users/Create", Form(
            ("Name", "Blocked User"),
            ("Email", "blocked@taskflow.test"),
            ("Password", "strong-password-123"),
            ("Profile", "2"),
            ("__RequestVerificationToken", antiforgery)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/Account/AccessDenied", createResponse.Headers.Location?.AbsolutePath);
        Assert.Equal(0, factory.Api.UserCreateCallCount);
        Assert.Equal(0, factory.Api.UsersListCallCount);
    }

    [Fact]
    public async Task ApiUnavailableDuringProfileValidationFailsClosedForAdminRoutes()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        factory.Api.CurrentUserStatusCode = StatusCodes.Status503ServiceUnavailable;

        using var response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", LocationPath(response));
        Assert.Equal(0, factory.Api.UsersListCallCount);
    }

    [Fact]
    public async Task ClaimsTransformationReplacesManagedClaimsWithoutDuplicatesOrRecursiveCalls()
    {
        var api = new FakeTaskFlowApiClient();
        var contextAccessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var transformation = new TaskFlowClaimsTransformation(api, contextAccessor);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "stale-id"),
            new Claim(ClaimTypes.NameIdentifier, "duplicate-id"),
            new Claim(ClaimTypes.Name, "Stale Admin"),
            new Claim(ClaimTypes.Role, nameof(PerfilUsuario.Administrador)),
            new Claim(ClaimTypes.Role, nameof(PerfilUsuario.Administrador)),
            new Claim("taskflow_access_token", "test-token-secret"),
            new Claim("taskflow_access_token", "duplicate-token"),
            new Claim("custom", "kept")
        ],
        "Cookies");

        var transformed = await transformation.TransformAsync(new ClaimsPrincipal(identity));

        Assert.Single(transformed.FindAll(ClaimTypes.NameIdentifier));
        Assert.Single(transformed.FindAll(ClaimTypes.Name));
        Assert.Single(transformed.FindAll(ClaimTypes.Email));
        Assert.Single(transformed.FindAll(ClaimTypes.Role));
        Assert.Single(transformed.FindAll("taskflow_access_token"));
        Assert.Equal("kept", transformed.FindFirst("custom")?.Value);
        Assert.Equal("test-token-secret", transformed.FindFirst("taskflow_access_token")?.Value);
        Assert.Equal(1, api.CurrentUserCallCount);
    }

    [Fact]
    public async Task UsersControllerRendersSharedApiErrorInsteadOfHttp500()
    {
        using var factory = new WebTestFactory();
        factory.Api.GetUserStatusCode = StatusCodes.Status503ServiceUnavailable;
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");

        using var response = await client.GetAsync($"/Users/Edit/{FakeTaskFlowApiClient.EmployeeId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Não foi possível carregar o usuário.", PlainText(html));
    }

    [Fact]
    public async Task ExpiredApiSession_ClearsCookieAndRedirectsToLogin()
    {
        using var factory = new WebTestFactory();
        using var client = factory.CreateClient(NoRedirects);
        await LoginAsync(client, "admin@taskflow.test");
        factory.Api.CurrentUserStatusCode = StatusCodes.Status401Unauthorized;

        using var response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", LocationPath(response));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("TaskFlow.Web.Auth=", StringComparison.Ordinal)
            && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        using var nextResponse = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, nextResponse.StatusCode);
        Assert.Equal("/Account/Login", LocationPath(nextResponse));
    }

    private static readonly WebApplicationFactoryClientOptions NoRedirects = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", Form(
            ("Email", email),
            ("Password", "not-sent-to-html"),
            ("__RequestVerificationToken", token)));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        Assert.True(match.Success, "Expected an antiforgery input in the returned form.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] values) =>
        new(values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value)));

    private static string? LocationPath(HttpResponseMessage response) =>
        response.Headers.Location is { IsAbsoluteUri: true } absolute
            ? absolute.AbsolutePath
            : response.Headers.Location?.OriginalString;

    private static string PlainText(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")).Replace("&nbsp;", " ", StringComparison.Ordinal);

    private sealed class WebTestFactory : WebApplicationFactory<TaskFlow.Web.Program>
    {
        private readonly FakeTaskFlowApiClient _api = new();
        private readonly string _environment;
        private readonly string? _apiBaseUrl;

        public WebTestFactory(string environment = "Development", string? apiBaseUrl = null)
        {
            _environment = environment;
            _apiBaseUrl = apiBaseUrl;
        }

        public FakeTaskFlowApiClient Api => _api;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
            if (_apiBaseUrl is not null)
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Api:BaseUrl"] = _apiBaseUrl
                    }));
            }

            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<ITaskFlowApiClient>();
                services.AddSingleton<ITaskFlowApiClient>(_api);
            });
        }
    }

    private sealed class FakeTaskFlowApiClient : ITaskFlowApiClient
    {
        public Task<ApiResult<TaskSummary>> GetTaskSummaryAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(new ApiResult<TaskSummary>(200, new TaskSummary(16, 8, 3, 5), null));
        public static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid EmployeeId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid TaskId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        public const string RowVersion = "AQIDBAUGBwg=";

        private PublicTask _task = new(
            TaskId,
            "Tarefa real de teste",
            "Conteúdo recebido da API.",
            StatusTarefa.Pendente,
            PrioridadeTarefa.Alta,
            DateTime.SpecifyKind(new DateTime(2026, 11, 11, 12, 0, 0), DateTimeKind.Utc),
            DateTime.SpecifyKind(new DateTime(2026, 11, 12, 13, 30, 0), DateTimeKind.Utc),
            AdminId,
            EmployeeId,
            RowVersion);

        public int TaskListStatusCode { get; set; } = StatusCodes.Status200OK;
        public int StatusUpdateCode { get; set; } = StatusCodes.Status200OK;
        public int UserCreateCode { get; set; } = StatusCodes.Status201Created;
        public int CurrentUserStatusCode { get; set; } = StatusCodes.Status200OK;
        public int GetUserStatusCode { get; set; } = StatusCodes.Status200OK;
        public PublicUser CurrentUser { get; set; } = new(
            AdminId,
            "Admin User",
            "admin@taskflow.test",
            PerfilUsuario.Administrador);
        public string? LastAuthorizationToken { get; private set; }
        public string? LastCreateTaskRequest { get; private set; }
        public string? LastUpdateTaskRequest { get; private set; }
        public string? LastUpdateUserRequest { get; private set; }
        public PublicTask TaskData
        {
            get => _task;
            set => _task = value;
        }
        public int LastTaskPage { get; private set; }
        public int LastTaskPageSize { get; private set; }
        public StatusTarefa? LastTaskStatus { get; private set; }
        public PrioridadeTarefa? LastTaskPriority { get; private set; }
        public bool LastWithoutResponsible { get; private set; }
        public string? LastTaskTitle { get; private set; }
        public StatusTarefa LastRequestedStatus { get; private set; }
        public string? LastRequestedRowVersion { get; private set; }
        public int CurrentUserCallCount { get; private set; }
        public int UsersListCallCount { get; private set; }
        public int UserCreateCallCount { get; private set; }

        public Task<ApiResult<LoginApiResponse>> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            if (email.StartsWith("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ApiResult<LoginApiResponse>(401, null, "E-mail ou senha incorretos."));
            }

            var isAdmin = email.StartsWith("admin", StringComparison.OrdinalIgnoreCase);
            var user = isAdmin
                ? new PublicUser(AdminId, "Admin User", "admin@taskflow.test", PerfilUsuario.Administrador)
                : new PublicUser(EmployeeId, "Employee User", "employee@taskflow.test", PerfilUsuario.Funcionario);
            CurrentUser = user;
            return Task.FromResult(new ApiResult<LoginApiResponse>(
                200,
                new LoginApiResponse("test-token-secret", "Bearer", DateTimeOffset.UtcNow.AddMinutes(40), user),
                null));
        }

        public Task<ApiResult<PublicUser>> GetCurrentUserAsync(string token, CancellationToken cancellationToken)
        {
            CurrentUserCallCount++;
            LastAuthorizationToken = token;
            return Task.FromResult(new ApiResult<PublicUser>(
                CurrentUserStatusCode,
                CurrentUserStatusCode == StatusCodes.Status200OK ? CurrentUser : null,
                CurrentUserStatusCode == StatusCodes.Status503ServiceUnavailable
                    ? "API indisponível."
                    : null));
        }

        public Task<ApiResult<PagedTasks>> GetTasksAsync(
            string token,
            int page,
            int pageSize,
            StatusTarefa? status,
            PrioridadeTarefa? priority,
            Guid? responsibleId,
            bool withoutResponsible,
            string? title,
            CancellationToken cancellationToken)
        {
            LastAuthorizationToken = token;
            LastTaskPage = page;
            LastTaskPageSize = pageSize;
            LastTaskStatus = status;
            LastTaskPriority = priority;
            LastWithoutResponsible = withoutResponsible;
            LastTaskTitle = title;
            if (TaskListStatusCode != StatusCodes.Status200OK)
            {
                return Task.FromResult(new ApiResult<PagedTasks>(
                    TaskListStatusCode,
                    null,
                    TaskListStatusCode == StatusCodes.Status503ServiceUnavailable
                        ? "A API está indisponível para o teste."
                        : "Sua sessão expirou."));
            }

            var total = page > 1 ? 26 : status switch
            {
                StatusTarefa.Pendente => 8,
                StatusTarefa.EmAndamento => 3,
                StatusTarefa.Concluida => 5,
                _ => 16
            };
            IReadOnlyList<PublicTask> items = [_task];
            return Task.FromResult(new ApiResult<PagedTasks>(200, new PagedTasks(items, page, pageSize, total), null));
        }

        public Task<ApiResult<PublicTask>> GetTaskAsync(string token, Guid id, CancellationToken cancellationToken)
        {
            LastAuthorizationToken = token;
            return Task.FromResult(id == TaskId
                ? new ApiResult<PublicTask>(200, _task, null)
                : new ApiResult<PublicTask>(404, null, "Não encontrado."));
        }

        public Task<ApiResult<PagedTaskHistory>> GetTaskHistoryAsync(
            string token,
            Guid id,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastAuthorizationToken = token;
            IReadOnlyList<PublicTaskHistory> items =
            [
                new(
                    Guid.Parse("40000000-0000-0000-0000-000000000004"),
                    TaskId,
                    AdminId,
                    "CriarTarefa",
                    DateTime.SpecifyKind(new DateTime(2026, 11, 11, 12, 0, 0), DateTimeKind.Utc),
                    "Tarefa criada.")
            ];
            return Task.FromResult(new ApiResult<PagedTaskHistory>(200, new PagedTaskHistory(items, page, pageSize, 1), null));
        }

        public Task<ApiResult<PublicTask>> CreateTaskAsync(
            string token,
            CreateTaskApiRequest request,
            CancellationToken cancellationToken)
        {
            LastAuthorizationToken = token;
            LastCreateTaskRequest = JsonSerializer.Serialize(
                request,
                request.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Task.FromResult(new ApiResult<PublicTask>(201, _task, null));
        }

        public Task<ApiResult<PublicTask>> UpdateTaskAsync(
            string token,
            Guid id,
            UpdateTaskApiRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateTaskRequest = JsonSerializer.Serialize(
                request,
                request.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Task.FromResult(new ApiResult<PublicTask>(
                StatusUpdateCode,
                StatusUpdateCode == 409 ? null : _task,
                "Conflito de teste."));
        }

        public Task<ApiResult<PublicTask>> UpdateTaskStatusAsync(
            string token,
            Guid id,
            UpdateTaskStatusApiRequest request,
            CancellationToken cancellationToken)
        {
            LastAuthorizationToken = token;
            LastRequestedStatus = request.Status;
            LastRequestedRowVersion = request.RowVersion;
            return Task.FromResult(new ApiResult<PublicTask>(
                StatusUpdateCode,
                StatusUpdateCode == StatusCodes.Status200OK ? _task : null,
                StatusUpdateCode == StatusCodes.Status409Conflict ? "Conflito atual da tarefa." : null));
        }

        public Task<ApiResult<PagedUsers>> GetUsersAsync(
            string token,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            UsersListCallCount++;
            IReadOnlyList<PublicUser> users =
            [
                new(AdminId, "Admin User", "admin@taskflow.test", PerfilUsuario.Administrador),
                new(EmployeeId, "Employee User", "employee@taskflow.test", PerfilUsuario.Funcionario)
            ];
            return Task.FromResult(new ApiResult<PagedUsers>(200, new PagedUsers(users, page, pageSize, users.Count), null));
        }

        public Task<ApiResult<PublicUser>> GetUserAsync(string token, Guid id, CancellationToken cancellationToken)
        {
            if (GetUserStatusCode != StatusCodes.Status200OK)
            {
                return Task.FromResult(new ApiResult<PublicUser>(
                    GetUserStatusCode,
                    null,
                    "Não foi possível carregar o usuário."));
            }

            var user = id == EmployeeId
                ? new PublicUser(EmployeeId, "Employee User", "employee@taskflow.test", PerfilUsuario.Funcionario)
                : new PublicUser(AdminId, "Admin User", "admin@taskflow.test", PerfilUsuario.Administrador);
            return Task.FromResult(new ApiResult<PublicUser>(200, user, null));
        }

        public Task<ApiResult<PublicUser>> CreateUserAsync(
            string token,
            CreateUserApiRequest request,
            CancellationToken cancellationToken)
        {
            UserCreateCallCount++;
            return
            Task.FromResult(new ApiResult<PublicUser>(
                UserCreateCode,
                UserCreateCode == StatusCodes.Status201Created
                    ? new PublicUser(Guid.NewGuid(), "New User", "new@taskflow.test", PerfilUsuario.Funcionario)
                    : null,
                UserCreateCode == StatusCodes.Status409Conflict ? "E-mail já cadastrado." : null));
        }

        public Task<ApiResult<PublicUser>> UpdateUserAsync(
            string token,
            Guid id,
            UpdateUserApiRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateUserRequest = JsonSerializer.Serialize(
                request,
                request.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Task.FromResult(new ApiResult<PublicUser>(
                200,
                new PublicUser(id, "Updated User", "updated@taskflow.test", PerfilUsuario.Funcionario),
                null));
        }
    }
}
