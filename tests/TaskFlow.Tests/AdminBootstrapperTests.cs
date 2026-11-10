using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Authentication;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class AdminBootstrapperTests
{
    private readonly SqlServerTestDatabase _database;

    public AdminBootstrapperTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Bootstrap_CreatesOneHashedAdministratorAndIsIdempotent()
    {
        await _database.ResetAsync();

        var created = await CreateAdminAsync("Primeiro Admin", "first-admin@taskflow.test", "Bootstrap-Password-42");
        var duplicate = await CreateAdminAsync("Outro Admin", "second-admin@taskflow.test", "Bootstrap-Password-43");

        Assert.True(created);
        Assert.False(duplicate);
        await using var context = _database.CreateContext();
        var admin = await context.Usuarios.SingleAsync();
        Assert.Equal(PerfilUsuario.Administrador, admin.Perfil);
        Assert.NotEqual("Bootstrap-Password-42", admin.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Usuario>().VerifyHashedPassword(admin, admin.PasswordHash, "Bootstrap-Password-42"));
    }

    private async Task<bool> CreateAdminAsync(string name, string email, string password)
    {
        await using var context = _database.CreateContext();
        var bootstrapper = new AdminBootstrapper(
            new TaskFlow.Infrastructure.Data.Repositories.UsuarioRepository(context),
            new TaskFlow.Infrastructure.Data.TaskFlowUnitOfWork(context),
            new PasswordHasher<Usuario>());
        return await bootstrapper.CreateFirstAdministratorAsync(name, email, password);
    }
}
