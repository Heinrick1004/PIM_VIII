using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TaskFlow.Web.Contracts;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

[Trait("Category", "Web")]
public sealed class TaskFlowApiClientTests
{
    [Fact]
    public async Task LoginAsync_SendsTheExistingEmailAndSenhaContract()
    {
        string? requestBody = null;
        using var httpClient = CreateClient(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new LoginApiResponse(
                "api-token",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(30),
                new PublicUser(Guid.NewGuid(), "Web User", "web@taskflow.test", PerfilUsuario.Funcionario)));
        });
        var client = new TaskFlowApiClient(httpClient);

        var result = await client.LoginAsync("web@taskflow.test", "password-from-form", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("web@taskflow.test", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("password-from-form", body.RootElement.GetProperty("senha").GetString());
        Assert.False(body.RootElement.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task TaskListAsync_SendsBearerAndAllEncodedFilters()
    {
        string? requestUri = null;
        string? authorization = null;
        using var httpClient = CreateClient(request =>
        {
            requestUri = request.RequestUri!.AbsoluteUri;
            authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new PagedTasks([], 2, 10, 0)));
        });
        var client = new TaskFlowApiClient(httpClient);
        var responsibleId = Guid.Parse("30000000-0000-0000-0000-000000000003");

        var result = await client.GetTasksAsync(
            "protected-api-token",
            2,
            10,
            StatusTarefa.EmAndamento,
            PrioridadeTarefa.Alta,
            responsibleId,
            true,
            "versão final",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Bearer protected-api-token", authorization);
        Assert.Contains("page=2", requestUri);
        Assert.Contains("status=2", requestUri);
        Assert.Contains("prioridade=3", requestUri);
        Assert.Contains($"responsavelId={responsibleId}", requestUri);
        Assert.Contains("semResponsavel=true", requestUri);
        Assert.Contains("titulo=vers%C3%A3o%20final", requestUri);
    }

    [Fact]
    public async Task StatusUpdateAsync_SendsPatchRowVersionAndReturnsConflictMessage()
    {
        HttpMethod? method = null;
        string? authorization = null;
        string? bodyText = null;
        using var httpClient = CreateClient(async request =>
        {
            method = request.Method;
            authorization = request.Headers.Authorization?.ToString();
            bodyText = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new { error = "A tarefa foi alterada por outra operação." })
            };
        });
        var client = new TaskFlowApiClient(httpClient);
        var id = Guid.Parse("30000000-0000-0000-0000-000000000003");

        var result = await client.UpdateTaskStatusAsync(
            "web-session-token",
            id,
            new UpdateTaskStatusApiRequest(StatusTarefa.Concluida, "AQIDBAUGBwg="),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, method);
        Assert.Equal("Bearer web-session-token", authorization);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("A tarefa foi alterada por outra operação.", result.Message);
        using var body = JsonDocument.Parse(bodyText!);
        Assert.Equal(3, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("AQIDBAUGBwg=", body.RootElement.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task ServerError_DoesNotExposeInternalDetails()
    {
        using var httpClient = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(new
            {
                detail = "Server=private;Database=TaskFlow;Password=secret-value;"
            })
        }));
        var client = new TaskFlowApiClient(httpClient);

        var result = await client.GetTasksAsync(
            "web-session-token", 1, 10, null, null, null, false, null, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Não foi possível concluir a operação. Tente novamente.", result.Message);
        Assert.DoesNotContain("private", result.Message);
        Assert.DoesNotContain("secret-value", result.Message);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsExplicitUnavailableResult()
    {
        using var httpClient = CreateClient(_ => throw new HttpRequestException("connection failed"));
        var client = new TaskFlowApiClient(httpClient);

        var result = await client.GetTasksAsync(
            "web-session-token", 1, 10, null, null, null, false, null, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("API TaskFlow está indisponível", result.Message);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) =>
        new(new CallbackHandler(sendAsync))
        {
            BaseAddress = new Uri("http://api.test")
        };

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode status, T body) =>
        new(status) { Content = JsonContent.Create(body) };

    private sealed class CallbackHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(request);
    }
}
