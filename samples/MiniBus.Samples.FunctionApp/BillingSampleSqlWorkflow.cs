using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Persistence.Sql;

namespace MiniBus.Samples.FunctionApp;

public static class BillingSampleSqlPersistence
{
    public const string EnabledSetting = "BillingSqlEnabled";
    public const string ConnectionSetting = "BillingSql";
    public const string SchemaSetting = "BillingSqlSchema";
    public const string DefaultSchemaName = "MiniBus";
    public const string LocalConnectionString =
        "Server=localhost,14333;Database=master;User Id=sa;Password=MiniBusEmulator!123;TrustServerCertificate=True;Encrypt=False";

    public static bool IsEnabled(IConfiguration configuration)
    {
        return bool.TryParse(configuration[EnabledSetting], out var enabled) && enabled;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionSetting];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set '{ConnectionSetting}' before enabling SQL persistence for the Billing sample.");
        }

        return connectionString;
    }

    public static string GetSchemaName(IConfiguration configuration)
    {
        return configuration[SchemaSetting] is { Length: > 0 } schemaName
            ? schemaName
            : DefaultSchemaName;
    }

    public static string GetCommandConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionSetting)
               ?? LocalConnectionString;
    }

    public static IConfiguration CreateCommandConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BillingTopology.ServiceBusConnectionSetting] =
                    BillingSampleServiceBusConnection.GetSeedConnectionString(),
                [EnabledSetting] = bool.TrueString,
                [ConnectionSetting] = GetCommandConnectionString()
            })
            .Build();
    }
}

public static class BillingSampleSqlSchemaApplier
{
    private const string ApplySchemaCommand = "--apply-sql-schema";

    public static bool IsApplySchemaCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0
               && string.Equals(args[0], ApplySchemaCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ApplyAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var scriptPaths = Directory.GetFiles(GetSchemaDirectory(), "*.sql");
        var invalidScript = scriptPaths
            .Select(Path.GetFileName)
            .FirstOrDefault(name => !IsVersionedSchemaScriptName(name));

        if (invalidScript is not null)
        {
            throw new InvalidOperationException(
                "MiniBus SQL schema scripts must use a three-digit migration prefix " +
                $"such as '001-'. Invalid script: {invalidScript}");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var scriptPath in scriptPaths.OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = await File
                .ReadAllTextAsync(scriptPath, cancellationToken)
                .ConfigureAwait(false);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return scriptPaths.Length;
    }

    private static string GetSchemaDirectory()
    {
        var schemaDirectory = Path.Combine(AppContext.BaseDirectory, "Schema");

        if (!Directory.Exists(schemaDirectory))
        {
            throw new DirectoryNotFoundException(
                $"MiniBus SQL schema scripts were not found at '{schemaDirectory}'.");
        }

        return schemaDirectory;
    }

    private static bool IsVersionedSchemaScriptName(string? fileName)
    {
        return fileName is { Length: > 4 }
               && char.IsDigit(fileName[0])
               && char.IsDigit(fileName[1])
               && char.IsDigit(fileName[2])
               && fileName[3] == '-';
    }
}

public static class BillingSampleOutboxDrainer
{
    private const string DrainCommand = "--drain-outbox";

    public static bool IsDrainCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0
               && string.Equals(args[0], DrainCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> DispatchPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingMiniBus(BillingSampleSqlPersistence.CreateCommandConfiguration());

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        return await provider
            .GetRequiredService<SqlMiniBusOutboxDispatcher>()
            .DispatchPendingAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
