using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "Http")]
[Trait("Category", "SqlServer")]
public sealed class AuthenticationEndpointTests
{
    private const string Password = "Correct-Horse-Battery-42";
    private readonly SqlServerTestDatabase _database;

    public AuthenticationEndpointTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Login_ValidaHashEmiteJwtComClaimsMinimasEMeRetornaUsuarioAtual()
    {
        await _database.ResetAsync();
        var user = await SeedUserAsync("Funcionario", "funcionario@taskflow.test", Password, PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(" FUNCIONARIO@TaskFlow.Test ", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);
        Assert.True(result.ExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(result.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(61));
        Assert.Equal(user.Id, result.User.Id);
        Assert.False(result.User.Email.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));

        var parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal(TestJwtConfiguration.Issuer, parsedToken.Issuer);
        Assert.Contains(TestJwtConfiguration.Audience, parsedToken.Audiences);
        Assert.Equal(user.Id.ToString(), parsedToken.Claims.Single(claim => claim.Type == "sub").Value);
        Assert.Equal(nameof(PerfilUsuario.Funcionario), parsedToken.Claims.Single(claim => claim.Type == "role").Value);
        Assert.DoesNotContain(parsedToken.Claims, claim => claim.Type.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parsedToken.Claims, claim => claim.Value.Contains(Password, StringComparison.Ordinal));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        using var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<PublicUserResponse>();
        Assert.NotNull(me);
        Assert.Equal(user.Id, me.Id);
        Assert.Equal("Funcionario", me.Nome);
        Assert.Equal(PerfilUsuario.Funcionario, me.Perfil);
    }

    [Fact]
    public async Task Login_RejeitaSenhaEEmailDesconhecidoComRespostaGenerica()
    {
        await _database.ResetAsync();
        await SeedUserAsync("Funcionario", "funcionario@taskflow.test", Password, PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("funcionario@taskflow.test", "Wrong-Password-For-Test"));
        using var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown@taskflow.test", Password));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        using var wrongPasswordBody = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        using var unknownEmailBody = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(
            wrongPasswordBody.RootElement.GetProperty("detail").GetString(),
            unknownEmailBody.RootElement.GetProperty("detail").GetString());
        Assert.Equal("E-mail ou senha incorretos.", unknownEmailBody.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_RejeitaTokensAdulteradosExpiradosERequisicoesAnonimas()
    {
        await _database.ResetAsync();
        var user = await SeedUserAsync("Administrador", "admin@taskflow.test", Password, PerfilUsuario.Administrador);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var anonymousResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var validToken = CreateToken(
            user.Id,
            nameof(PerfilUsuario.Administrador),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddHours(1));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TamperSubject(validToken));
        using var tamperedResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, tamperedResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(user.Id, nameof(PerfilUsuario.Administrador), DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)));
        using var expiredResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(
                user.Id,
                nameof(PerfilUsuario.Administrador),
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddHours(1),
                issuer: "UnexpectedIssuer"));
        using var wrongIssuer = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongIssuer.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(
                user.Id,
                nameof(PerfilUsuario.Administrador),
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddHours(1),
                audience: "UnexpectedAudience"));
        using var wrongAudience = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
    }

    [Fact]
    public async Task UserManagement_ExigeAdministradorERevalidaPerfilAtualDoToken()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Administrador", "admin@taskflow.test", Password, PerfilUsuario.Administrador);
        var employee = await SeedUserAsync("Funcionario", "funcionario@taskflow.test", Password, PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var anonymousClient = factory.CreateClient();

        using var noToken = await anonymousClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        using var employeeClient = await LoginAsync(factory, "funcionario@taskflow.test");
        using var forbidden = await employeeClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var adminClient = await LoginAsync(factory, "admin@taskflow.test");
        using var allowed = await adminClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        await using (var context = _database.CreateContext())
        {
            var storedAdmin = await context.Usuarios.SingleAsync(user => user.Id == admin.Id);
            storedAdmin.UpdateProfile(storedAdmin.Nome, storedAdmin.Email, PerfilUsuario.Funcionario);
            await context.SaveChangesAsync();
        }

        using var staleAdminToken = await adminClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, staleAdminToken.StatusCode);
        Assert.NotEqual(admin.Id, employee.Id);
    }

    [Fact]
    public async Task UserManagement_CreatesPagesReadsUpdatesAndRejectsDuplicateEmail()
    {
        await _database.ResetAsync();
        var admin = await SeedUserAsync("Administrador", "admin@taskflow.test", Password, PerfilUsuario.Administrador);
        await SeedUserAsync("Funcionario", "employee@taskflow.test", Password, PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, "admin@taskflow.test");

        using var created = await client.PostAsJsonAsync("/api/users", new
        {
            nome = "Novo Funcionário",
            email = "novo@taskflow.test",
            senha = Password,
            perfil = PerfilUsuario.Funcionario
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdUser = await created.Content.ReadFromJsonAsync<PublicUserResponse>();
        Assert.NotNull(createdUser);
        var createdJson = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", createdJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, createdJson, StringComparison.Ordinal);

        using var invalidUser = await client.PostAsJsonAsync("/api/users", new
        {
            nome = "Perfil inválido",
            email = "invalid-profile@taskflow.test",
            senha = "short",
            perfil = (PerfilUsuario)99
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUser.StatusCode);

        await using (var context = _database.CreateContext())
        {
            var storedUser = await context.Usuarios.SingleAsync(user => user.Id == createdUser.Id);
            Assert.NotEqual(Password, storedUser.PasswordHash);
            Assert.Equal(PasswordVerificationResult.Success,
                new PasswordHasher<Usuario>().VerifyHashedPassword(storedUser, storedUser.PasswordHash, Password));
        }

        using var duplicate = await client.PostAsJsonAsync("/api/users", new
        {
            nome = "Duplicado",
            email = "NOVO@TASKFLOW.TEST",
            senha = Password,
            perfil = PerfilUsuario.Funcionario
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var list = await client.GetAsync("/api/users?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedUsersResponse>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Single(page.Items);
        Assert.Equal(3, page.TotalCount);
        Assert.DoesNotContain("PasswordHash", await list.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var invalidPage = await client.GetAsync("/api/users?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        using var getUser = await client.GetAsync($"/api/users/{createdUser.Id}");
        Assert.Equal(HttpStatusCode.OK, getUser.StatusCode);
        using var notFound = await client.GetAsync($"/api/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        using var lastAdminDemotion = await client.PutAsJsonAsync($"/api/users/{admin.Id}", new
        {
            nome = admin.Nome,
            email = admin.Email,
            perfil = PerfilUsuario.Funcionario
        });
        Assert.Equal(HttpStatusCode.Conflict, lastAdminDemotion.StatusCode);

        using var update = await client.PutAsJsonAsync($"/api/users/{createdUser.Id}", new
        {
            nome = "Nome Atualizado",
            email = "novo@taskflow.test",
            perfil = PerfilUsuario.Administrador
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedUser = await update.Content.ReadFromJsonAsync<PublicUserResponse>();
        Assert.NotNull(updatedUser);
        Assert.Equal("Nome Atualizado", updatedUser.Nome);
        Assert.Equal(PerfilUsuario.Administrador, updatedUser.Perfil);

        using var duplicateUpdate = await client.PutAsJsonAsync($"/api/users/{createdUser.Id}", new
        {
            nome = "Nome Atualizado",
            email = "EMPLOYEE@TASKFLOW.TEST",
            perfil = PerfilUsuario.Administrador
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateUpdate.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("short")]
    [InlineData("too-long")]
    public async Task UserManagement_CreateRejectsMissingOrInvalidPassword(string passwordCase)
    {
        await _database.ResetAsync();
        await SeedUserAsync("Administrador", "admin@taskflow.test", Password, PerfilUsuario.Administrador);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, "admin@taskflow.test");

        var request = new Dictionary<string, object?>
        {
            ["nome"] = "Senha inválida",
            ["email"] = "invalid-password@taskflow.test",
            ["perfil"] = PerfilUsuario.Funcionario
        };

        if (passwordCase != "missing")
        {
            request["senha"] = passwordCase switch
            {
                "null" => null,
                "empty" => string.Empty,
                "short" => new string('x', PasswordRules.MinimumLength - 1),
                "too-long" => new string('x', PasswordRules.MaximumLength + 1),
                _ => throw new ArgumentOutOfRangeException(nameof(passwordCase), passwordCase, "Unknown password case.")
            };
        }

        using var response = await client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = payload.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("senha", out var passwordErrors));
        Assert.NotEmpty(passwordErrors.EnumerateArray());

        await using var context = _database.CreateContext();
        Assert.Equal(0, await context.Usuarios.CountAsync(user => user.Email == "invalid-password@taskflow.test"));
    }

    [Fact]
    public async Task Employee_CannotCreateUsers()
    {
        await _database.ResetAsync();
        await SeedUserAsync("Funcionario", "funcionario@taskflow.test", Password, PerfilUsuario.Funcionario);
        await using var factory = CreateFactory();
        using var client = await LoginAsync(factory, "funcionario@taskflow.test");

        using var response = await client.PostAsJsonAsync("/api/users", new
        {
            nome = "Tentativa",
            email = "tentativa@taskflow.test",
            senha = Password,
            perfil = PerfilUsuario.Administrador
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_RehashesAValidLegacyPasswordHash()
    {
        await _database.ResetAsync();
        Guid userId;
        await using (var context = _database.CreateContext())
        {
            var user = new Usuario(Guid.NewGuid(), "Legacy", "legacy@taskflow.test", "temporary", PerfilUsuario.Funcionario);
            var legacyHasher = new PasswordHasher<Usuario>(Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
            }));
            user.UpdatePasswordHash(legacyHasher.HashPassword(user, Password));
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("legacy@taskflow.test", Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verificationContext = _database.CreateContext();
        var updated = await verificationContext.Usuarios.SingleAsync(item => item.Id == userId);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<Usuario>().VerifyHashedPassword(updated, updated.PasswordHash, Password));
    }

    private async Task<Usuario> SeedUserAsync(string name, string email, string password, PerfilUsuario profile)
    {
        await using var context = _database.CreateContext();
        var user = new Usuario(Guid.NewGuid(), name, email, "temporary", profile);
        user.UpdatePasswordHash(new PasswordHasher<Usuario>().HashPassword(user, password));
        context.Usuarios.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(result);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return client;
    }

    private WebApplicationFactory<Program> CreateFactory() =>
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
        });

    private static string CreateToken(
        Guid userId,
        string role,
        DateTime notBefore,
        DateTime expires,
        string? issuer = null,
        string? audience = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(TestJwtConfiguration.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer ?? TestJwtConfiguration.Issuer,
            audience ?? TestJwtConfiguration.Audience,
            [new Claim("sub", userId.ToString()), new Claim("role", role)],
            notBefore,
            expires,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string TamperSubject(string token)
    {
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var subject = Guid.Parse(
            System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
        var replacement = subject == Guid.Empty ? Guid.NewGuid() : Guid.Empty;
        var tamperedPayload = payload.Replace(subject.ToString(), replacement.ToString(), StringComparison.Ordinal);
        parts[1] = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(tamperedPayload));
        return string.Join('.', parts);
    }
}
