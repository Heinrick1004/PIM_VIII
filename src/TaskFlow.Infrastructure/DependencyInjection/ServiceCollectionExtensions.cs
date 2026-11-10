using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Contracts;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Data.Repositories;

namespace TaskFlow.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskFlowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TaskFlowDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("TaskFlow");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Configure a connection string 'TaskFlow' before resolving persistence.");
            }

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITarefaRepository, TarefaRepository>();
        services.AddScoped<IHistoricoTarefaRepository, HistoricoTarefaRepository>();
        services.AddScoped<IUnitOfWork, TaskFlowUnitOfWork>();

        return services;
    }
}
