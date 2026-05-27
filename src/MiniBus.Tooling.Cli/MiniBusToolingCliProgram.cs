using MiniBus.Tooling.Core;
using MiniBus.Tooling.Sql;

namespace MiniBus.Tooling.Cli;

public static class MiniBusToolingCliProgram
{
    public static Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        var parsed = MiniBusToolingCliParser.Parse(args);

        if (parsed.HelpRequested)
        {
            output.WriteLine(MiniBusToolingCliHelp.Text);
            return Task.FromResult(0);
        }

        if (!parsed.Options.TryGetValue("connection-string", out var connectionString)
            || string.IsNullOrWhiteSpace(connectionString))
        {
            error.WriteLine("Missing required option: --connection-string <value>");
            return Task.FromResult(1);
        }

        var sqlOptions = new MiniBusSqlToolingOptions
        {
            ConnectionString = connectionString,
            SchemaName = parsed.Options.GetValueOrDefault("schema") ?? "MiniBus"
        };
        var sqlReader = new SqlMiniBusToolingReader(sqlOptions);
        var app = new MiniBusToolingCliApplication(
            sqlReader,
            sqlReader,
            sqlReader,
            sqlReader,
            new UnsupportedOutboxDrainAction(
                "Standalone CLI drain requires an application-configured SqlMiniBusOutboxDispatcher and transport dispatcher."));

        return app.RunAsync(parsed, output, error, cancellationToken);
    }

    private sealed class UnsupportedOutboxDrainAction : IMiniBusOutboxDrainAction
    {
        private readonly string _reason;

        public UnsupportedOutboxDrainAction(string reason)
        {
            _reason = reason;
        }

        public Task<MiniBusOutboxDrainResult> DrainAsync(
            MiniBusOutboxDrainRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MiniBusOutboxDrainResult.Unsupported(_reason));
        }
    }
}
