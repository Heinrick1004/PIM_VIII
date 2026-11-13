using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Tests;

[Collection(SqlServerTestCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerProgrammabilityTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task FunctionProcedureAndTrigger_EnforceExpectedBehaviorOnSqlServer()
    {
        await database.ResetAsync();
        var creator = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var other = Guid.NewGuid();
        var unlinked = Guid.NewGuid();
        var taskCreated = Guid.NewGuid();
        var taskAssigned = Guid.NewGuid();
        var taskBoth = Guid.NewGuid();
        var taskOther = Guid.NewGuid();
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var seed = new SqlCommand("""
            INSERT dbo.Usuarios (Id,Nome,Email,PasswordHash,Perfil) VALUES
             (@creator,N'Creator',N'creator@test.local',N'hash',2),
             (@assignee,N'Assignee',N'assignee@test.local',N'hash',2),
             (@other,N'Other',N'other@test.local',N'hash',2),
             (@unlinked,N'Unlinked',N'unlinked@test.local',N'hash',2);
            INSERT dbo.Tarefas (Id,Titulo,Status,Prioridade,CriadaEmUtc,CriadorId,ResponsavelId) VALUES
             (@created,N'Created',1,1,SYSUTCDATETIME(),@creator,@assignee),
             (@assigned,N'Assigned',2,1,SYSUTCDATETIME(),@other,@creator),
             (@both,N'Both',3,1,SYSUTCDATETIME(),@creator,@creator),
             (@otherTask,N'Other task',3,1,SYSUTCDATETIME(),@other,NULL);
            """, connection))
        {
            seed.Parameters.AddWithValue("@creator", creator);
            seed.Parameters.AddWithValue("@assignee", assignee);
            seed.Parameters.AddWithValue("@other", other);
            seed.Parameters.AddWithValue("@unlinked", unlinked);
            seed.Parameters.AddWithValue("@created", taskCreated);
            seed.Parameters.AddWithValue("@assigned", taskAssigned);
            seed.Parameters.AddWithValue("@both", taskBoth);
            seed.Parameters.AddWithValue("@otherTask", taskOther);
            await seed.ExecuteNonQueryAsync();
        }

        await using (var function = new SqlCommand("SELECT Id FROM dbo.fn_TarefasVisiveis(@userId);", connection))
        {
            function.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.UniqueIdentifier) { Value = creator });
            await using var reader = await function.ExecuteReaderAsync();
            var ids = new List<Guid>();
            while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
            Assert.Equal(3, ids.Count);
            Assert.Contains(taskCreated, ids);
            Assert.Contains(taskAssigned, ids);
            Assert.Contains(taskBoth, ids);
            Assert.DoesNotContain(taskOther, ids);
        }
        await using (var function = new SqlCommand("SELECT COUNT(*) FROM dbo.fn_TarefasVisiveis(@userId);", connection))
        {
            function.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.UniqueIdentifier) { Value = assignee });
            Assert.Equal(1, await function.ExecuteScalarAsync());
        }
        await using (var function = new SqlCommand("SELECT COUNT(*) FROM dbo.fn_TarefasVisiveis(@userId);", connection))
        {
            function.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.UniqueIdentifier) { Value = Guid.Empty });
            Assert.Equal(0, await function.ExecuteScalarAsync());
        }
        await using (var function = new SqlCommand("SELECT COUNT(*) FROM dbo.fn_TarefasVisiveis(@userId);", connection))
        {
            function.Parameters.Add(new SqlParameter("@userId", System.Data.SqlDbType.UniqueIdentifier) { Value = unlinked });
            Assert.Equal(0, await function.ExecuteScalarAsync());
        }

        async Task<int[]> Summary(Guid id, bool admin)
        {
            await using var command = new SqlCommand("dbo.usp_TarefasResumo", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.Add(new SqlParameter("@UsuarioId", System.Data.SqlDbType.UniqueIdentifier) { Value = id });
            command.Parameters.Add(new SqlParameter("@EhAdministrador", System.Data.SqlDbType.Bit) { Value = admin });
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return [reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)];
        }
        var creatorSummary = await Summary(creator, false);
        var adminSummary = await Summary(unlinked, true);
        var emptySummary = await Summary(unlinked, false);
        Assert.Equal(new[] { 3, 1, 1, 1 }, creatorSummary);
        Assert.Equal(new[] { 4, 1, 1, 2 }, adminSummary);
        Assert.Equal(new[] { 0, 0, 0, 0 }, emptySummary);

        await using (var updateCreator = new SqlCommand("UPDATE dbo.Tarefas SET CriadorId=@other WHERE Id=@id;", connection))
        {
            updateCreator.Parameters.AddWithValue("@other", other);
            updateCreator.Parameters.AddWithValue("@id", taskCreated);
            var exception = await Assert.ThrowsAsync<SqlException>(() => updateCreator.ExecuteNonQueryAsync());
            Assert.Equal(51001, exception.Number);
        }
        await using (var updateCreatedAt = new SqlCommand("UPDATE dbo.Tarefas SET CriadaEmUtc=DATEADD(day,1,CriadaEmUtc) WHERE Id=@id;", connection))
        {
            updateCreatedAt.Parameters.AddWithValue("@id", taskCreated);
            var exception = await Assert.ThrowsAsync<SqlException>(() => updateCreatedAt.ExecuteNonQueryAsync());
            Assert.Equal(51001, exception.Number);
        }
        await using (var update = new SqlCommand("UPDATE dbo.Tarefas SET Titulo=N'Updated', Status=2, ResponsavelId=@assignee WHERE Id IN (@id,@secondId);", connection))
        {
            update.Parameters.AddWithValue("@assignee", assignee);
            update.Parameters.AddWithValue("@id", taskCreated);
            update.Parameters.AddWithValue("@secondId", taskAssigned);
            Assert.Equal(2, await update.ExecuteNonQueryAsync());
        }
        Assert.Equal(0, await new SqlCommand("SELECT COUNT(*) FROM dbo.HistoricoTarefas;", connection).ExecuteScalarAsync());
    }

    [Fact]
    public async Task MigrationDownRemovesProgrammabilityObjectsAndUpRestoresThem()
    {
        const string priorMigration = "20261110221054_DatabaseDefaults";
        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync(priorMigration);
            var remaining = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.objects WHERE object_id IN (OBJECT_ID(N'dbo.fn_TarefasVisiveis'), OBJECT_ID(N'dbo.usp_TarefasResumo'), OBJECT_ID(N'dbo.TR_Tarefas_ProtegeDadosCriacao'))").SingleAsync();
            Assert.Equal(0, remaining);
        }
        finally
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
        }
    }
}
