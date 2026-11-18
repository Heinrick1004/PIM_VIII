using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Desktop;

namespace TaskFlow.Desktop.Tests;

public sealed class ApiClientTests
{
    private static readonly Uri BaseUri = new("https://taskflow.test/");

    [Fact]
    public async Task LoginDesserializaContratoReal()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("/api/auth/login", request.RequestUri!.PathAndQuery);
            var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
            Assert.Equal("user@example.test", body.RootElement.GetProperty("email").GetString());
            Assert.Equal("secret", body.RootElement.GetProperty("senha").GetString());
            return Task.FromResult(Json("""{"accessToken":"jwt","tokenType":"Bearer","expiresAtUtc":"2030-01-01T00:00:00Z","user":{"id":"8a403365-430f-4744-a7b6-a679f60d79a5","nome":"Teste","email":"user@example.test","perfil":"Funcionario"}}"""));
        });
        using var client = new TaskFlowApiClient(BaseUri, handler);
        var result = await client.LoginAsync("user@example.test", "secret");
        Assert.Equal("jwt", result.AccessToken);
        Assert.Equal("Funcionario", result.User.Perfil);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)] [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)] [InlineData(HttpStatusCode.Conflict)]
    public async Task MapeiaErrosHttp(HttpStatusCode status)
    {
        using var client = new TaskFlowApiClient(BaseUri, new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
        var exception = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("a@b.test", "x"));
        Assert.Equal(status, exception.Status);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public async Task AtualizacaoEnviaRowVersionEBearer()
    {
        const string id = "8a403365-430f-4744-a7b6-a679f60d79a5";
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
            Assert.Equal($"/api/tasks/{id}/status", request.RequestUri!.PathAndQuery);
            var json = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result).RootElement;
            Assert.Equal("Concluida", json.GetProperty("status").GetString());
            Assert.Equal("AQIDBAUGBwg=", json.GetProperty("rowVersion").GetString());
            return Task.FromResult(Json($"{{\"id\":\"{id}\",\"titulo\":\"T\",\"descricao\":null,\"status\":\"Concluida\",\"prioridade\":\"Media\",\"criadaEmUtc\":\"2026-01-01T00:00:00Z\",\"prazoUtc\":null,\"criadorId\":\"{id}\",\"responsavelId\":null,\"rowVersion\":\"CAcGBQQDAgE=\"}}"));
        });
        using var client = new TaskFlowApiClient(BaseUri, handler);
        var result = await client.UpdateStatusAsync("token", Guid.Parse(id), "Concluida", "AQIDBAUGBwg=");
        Assert.Equal("Concluida", result.Status);
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
