using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Authentication;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Data.Repositories;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class DemoAdminSeederTests
{
    private readonly SqlServerTestDatabase _database;

    public DemoAdminSeederTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task EnsureAdministrator_CreatesHashedAccountAndDoesNotOverwriteIt()
    {
        await _database.ResetAsync();
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var replacementPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var created = await EnsureAdminAsync(
            "Administrador de Demonstra\u00e7\u00e3o",
            "adm@gmail.com",
            password);
        var duplicate = await EnsureAdminAsync(
            "Outro Nome",
            "ADM@gmail.com",
            replacementPassword);

        Assert.True(created);
        Assert.False(duplicate);
        await using var context = _database.CreateContext();
        var admin = await context.Usuarios.SingleAsync();
        Assert.Equal("Administrador de Demonstra\u00e7\u00e3o", admin.Nome);
        Assert.Equal("adm@gmail.com", admin.Email);
        Assert.Equal(PerfilUsuario.Administrador, admin.Perfil);
        Assert.NotEqual(password, admin.PasswordHash);
        var hasher = new PasswordHasher<Usuario>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, password));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, replacementPassword));
    }

    [Fact]
    public async Task EnsureAdministrator_DoesNotPromoteExistingNonAdmin()
    {
        await _database.ResetAsync();
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var hasher = new PasswordHasher<Usuario>();
        var employee = new Usuario(
            Guid.NewGuid(),
            "Funcion\u00e1rio",
            "adm@gmail.com",
            "placeholder",
            PerfilUsuario.Funcionario);
        employee.UpdatePasswordHash(hasher.HashPassword(employee, "Employee-Password-42"));
        await using (var context = _database.CreateContext())
        {
            context.Usuarios.Add(employee);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EnsureAdminAsync("Administrador", "adm@gmail.com", password));

        await using var verifyContext = _database.CreateContext();
        var persisted = await verifyContext.Usuarios.SingleAsync();
        Assert.Equal(PerfilUsuario.Funcionario, persisted.Perfil);
        Assert.Equal(employee.PasswordHash, persisted.PasswordHash);
    }

    private async Task<bool> EnsureAdminAsync(string name, string email, string password)
    {
        await using var context = _database.CreateContext();
        var seeder = new DemoAdminSeeder(
            new UsuarioRepository(context),
            new TaskFlowUnitOfWork(context),
            new PasswordHasher<Usuario>());
        return await seeder.EnsureAdministratorAsync(name, email, password);
    }
}
