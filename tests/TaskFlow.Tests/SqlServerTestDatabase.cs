using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Tests;

[CollectionDefinition("SQL Server integration", DisableParallelization = true)]
public sealed class SqlServerTestCollection : ICollectionFixture<SqlServerTestDatabase>
{
    public const string Name = "SQL Server integration";
}

public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    private readonly string _masterConnectionString;
    private bool _databaseCreated;

    public SqlServerTestDatabase()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable("TASKFLOW_TEST_SQLSERVER_CONNECTION_STRING")
            ?? "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;";
        var builder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = "master",
            Pooling = false
        };

        _masterConnectionString = builder.ConnectionString;
        DatabaseName = $"TaskFlowIntegrationTests_{Guid.NewGuid():N}";
        var testDatabaseBuilder = new SqlConnectionStringBuilder(builder.ConnectionString)
        {
            InitialCatalog = DatabaseName
        };
        ConnectionString = testDatabaseBuilder.ConnectionString;
    }

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public async Task InitializeAsync()
    {
        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var existenceCheck = new SqlCommand("SELECT DB_ID(@databaseName);", connection);
            existenceCheck.Parameters.AddWithValue("@databaseName", DatabaseName);
            if (await existenceCheck.ExecuteScalarAsync() is not DBNull and not null)
            {
                throw new InvalidOperationException("The generated SQL integration-test database name already exists.");
            }

            await using var createDatabase = new SqlCommand($"CREATE DATABASE [{DatabaseName}];", connection);
            await createDatabase.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }

        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DropDatabaseAsync();
            throw;
        }
    }

    public TaskFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskFlowDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new TaskFlowDbContext(options);
    }

    public async Task ResetAsync()
    {
        EnsureOwnedDatabase();
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "DELETE FROM dbo.HistoricoTarefas; DELETE FROM dbo.Tarefas; DELETE FROM dbo.Usuarios;",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync();
    }

    private async Task DropDatabaseAsync()
    {
        if (!_databaseCreated)
        {
            return;
        }

        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];",
            connection);
        await command.ExecuteNonQueryAsync();
        _databaseCreated = false;
    }

    private void EnsureOwnedDatabase()
    {
        if (!_databaseCreated || !DatabaseName.StartsWith("TaskFlowIntegrationTests_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The integration-test fixture does not own the requested database.");
        }
    }
}
