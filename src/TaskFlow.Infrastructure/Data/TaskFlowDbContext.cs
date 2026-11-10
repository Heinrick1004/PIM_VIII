using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data;

public sealed class TaskFlowDbContext : DbContext
{
    public TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<HistoricoTarefa> HistoricoTarefas => Set<HistoricoTarefa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException sqlException
            && sqlException.Number is 2601 or 2627)
        {
            throw new PersistenceConstraintException(PersistenceConstraintKind.Unique, exception);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException sqlException
            && sqlException.Number == 547)
        {
            throw new PersistenceConstraintException(PersistenceConstraintKind.ForeignKeyOrCheck, exception);
        }
    }
}
