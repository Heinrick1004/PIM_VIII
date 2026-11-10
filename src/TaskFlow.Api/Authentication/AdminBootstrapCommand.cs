using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.DependencyInjection;

namespace TaskFlow.Api.Authentication;

public static class AdminBootstrapCommand
{
    public static async Task RunAsync(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("O bootstrap administrativo só pode ser executado em Development.");
        }

        builder.Services.AddTaskFlowInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        builder.Services.AddScoped<AdminBootstrapper>();

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        if (!string.Equals(context.Database.GetDbConnection().Database, "TaskFlowDev", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("O bootstrap só pode usar o banco de desenvolvimento TaskFlowDev.");
        }

        if (!await context.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("Não foi possível conectar ao banco TaskFlowDev.");
        }

        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException("O bootstrap exige entrada interativa para não expor a senha.");
        }

        Console.Write("Nome do administrador: ");
        var name = Console.ReadLine();
        Console.Write("E-mail do administrador: ");
        var email = Console.ReadLine();
        var password = ReadHiddenPassword("Senha inicial: ");
        var confirmation = ReadHiddenPassword("Confirme a senha: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("As senhas não correspondem.");
        }

        var created = await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>()
            .CreateFirstAdministratorAsync(name ?? string.Empty, email ?? string.Empty, password);
        Console.WriteLine(created
            ? "Administrador inicial criado."
            : "Já existe um administrador; nenhuma alteração foi realizada.");
    }

    private static string ReadHiddenPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
