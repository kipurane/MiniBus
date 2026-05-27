using Testcontainers.MsSql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string? _externalConnectionString;
    private MsSqlContainer? _container;
    private Exception? _startupException;

    public SqlServerFixture()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable(
            SqlServerTestSettings.ConnectionStringEnvironmentVariable);
    }

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            return;
        }

        try
        {
            _container = new MsSqlBuilder()
                .WithImage(SqlServerTestSettings.SqlServerImage)
                .WithCreateParameterModifier(parameters =>
                {
                    parameters.Platform = "linux/amd64";
                })
                .Build();

            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            _startupException = exception;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    internal async Task<SqlServerTestDatabase> CreateDatabaseAsync()
    {
        if (_startupException is not null)
        {
            throw new InvalidOperationException(
                $"SQL Server Testcontainers startup failed for {SqlServerTestSettings.SqlServerImage}. " +
                $"Set {SqlServerTestSettings.ConnectionStringEnvironmentVariable} to use an external SQL Server/Azure SQL database. " +
                $"Original error: {_startupException.Message}");
        }

        var connectionString = !string.IsNullOrWhiteSpace(_externalConnectionString)
            ? _externalConnectionString
            : _container?.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {SqlServerTestSettings.ConnectionStringEnvironmentVariable} or enable Docker to run SQL tooling tests.");
        }

        var database = new SqlServerTestDatabase(
            connectionString,
            $"MiniBusToolingTest_{Guid.NewGuid():N}");
        await database.ApplySchemaAsync();
        return database;
    }
}
