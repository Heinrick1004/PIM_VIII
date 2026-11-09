using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TaskFlow.Tests;

public sealed class ApiEndpointTests
{
    private static readonly DateTimeOffset Instant = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Health_Retorna200ComStatusETimestampUtcDoRelogio()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(Instant.UtcDateTime, payload.RootElement.GetProperty("timestampUtc").GetDateTime());
        Assert.Equal(DateTimeKind.Utc, payload.RootElement.GetProperty("timestampUtc").GetDateTime().Kind);
    }

    [Fact]
    public async Task Info_RetornaMetadadosDaVersaoInicial()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TaskFlow", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("0.1.0", payload.RootElement.GetProperty("version").GetString());
        Assert.Equal("API inicial do sistema de gestão de tarefas internas da Alunos Tech.",
            payload.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Swagger_EmDesenvolvimentoDescreveOsDoisEndpoints()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = payload.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/health", out _));
        Assert.True(paths.TryGetProperty("/api/info", out _));
    }

    [Theory]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/index.html")]
    public async Task Swagger_EmProducaoNaoEstaDisponivel(string path)
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            });
        });

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Instant;
    }
}
