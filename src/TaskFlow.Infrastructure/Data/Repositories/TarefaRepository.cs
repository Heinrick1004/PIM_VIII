using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Data.Repositories;

public sealed class TarefaRepository : ITarefaRepository
{
    private readonly TaskFlowDbContext _context;

    public TarefaRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tarefa tarefa, CancellationToken cancellationToken = default)
    {
        await _context.Tarefas.AddAsync(tarefa, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Tarefa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Tarefas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<TaskSummary> GetSummaryAsync(Guid usuarioId, bool ehAdministrador, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_TarefasResumo";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@UsuarioId", SqlDbType.UniqueIdentifier) { Value = usuarioId });
            command.Parameters.Add(new SqlParameter("@EhAdministrador", SqlDbType.Bit) { Value = ehAdministrador });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("A procedure de resumo não retornou linha.");
            return new TaskSummary(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        }
        finally { if (openedHere) await connection.CloseAsync(); }
    }

    public async Task<(IReadOnlyList<Tarefa> Tasks, int TotalCount)> ListPageAsync(
        int page,
        int pageSize,
        Guid? visibleUserId,
        StatusTarefa? status = null,
        PrioridadeTarefa? prioridade = null,
        Guid? responsavelId = null,
        bool semResponsavel = false,
        string? titulo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, 1_000_000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        var query = _context.Tarefas.AsNoTracking().AsQueryable();

        if (visibleUserId is Guid userId)
        {
            query = query.Where(task => task.CriadorId == userId || task.ResponsavelId == userId);
        }

        if (status.HasValue)
        {
            query = query.Where(task => task.Status == status.Value);
        }

        if (prioridade.HasValue)
        {
            query = query.Where(task => task.Prioridade == prioridade.Value);
        }

        if (responsavelId.HasValue)
        {
            query = query.Where(task => task.ResponsavelId == responsavelId.Value);
        }

        if (semResponsavel)
        {
            query = query.Where(task => task.ResponsavelId == null);
        }

        if (!string.IsNullOrWhiteSpace(titulo))
        {
            var normalized = titulo.Trim();
            query = query.Where(task => task.Titulo.Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var tasks = await query
            .OrderByDescending(task => task.CriadaEmUtc)
            .ThenByDescending(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (tasks, totalCount);
    }

    public async Task<IReadOnlyList<Tarefa>> ListAsync(
        int skip,
        int take,
        StatusTarefa? status = null,
        Guid? responsavelId = null,
        string? titulo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var query = _context.Tarefas.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (responsavelId.HasValue)
        {
            query = query.Where(x => x.ResponsavelId == responsavelId.Value);
        }

        if (!string.IsNullOrWhiteSpace(titulo))
        {
            var normalized = titulo.Trim();
            query = query.Where(x => x.Titulo.Contains(normalized));
        }

        return await query
            .OrderBy(x => x.CriadaEmUtc)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tarefa tarefa, CancellationToken cancellationToken = default)
    {
        _context.Entry(tarefa).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
