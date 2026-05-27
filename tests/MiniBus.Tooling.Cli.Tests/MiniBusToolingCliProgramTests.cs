using MiniBus.Tooling.Cli;
using Xunit;

namespace MiniBus.Tooling.Cli.Tests;

public sealed class MiniBusToolingCliProgramTests
{
    [Fact]
    public void Parse_CapturesArgumentsOptionsAndHelpFlag()
    {
        var parsed = MiniBusToolingCliParser.Parse(new[]
        {
            "show",
            "message",
            "--message-id",
            "message-1",
            "--limit",
            "10",
            "--help"
        });

        Assert.Equal(new[] { "show", "message" }, parsed.Arguments);
        Assert.Equal("message-1", parsed.Options["message-id"]);
        Assert.Equal("10", parsed.Options["limit"]);
        Assert.True(parsed.HelpRequested);
    }

    [Fact]
    public void Parse_PreservesValuelessOptions()
    {
        var parsed = MiniBusToolingCliParser.Parse(new[]
        {
            "outbox",
            "list",
            "--format",
            "json",
            "--status"
        });

        Assert.Equal(new[] { "outbox", "list" }, parsed.Arguments);
        Assert.Equal("json", parsed.Options["format"]);
        Assert.True(parsed.Options.ContainsKey("status"));
        Assert.Null(parsed.Options["status"]);
        Assert.False(parsed.HelpRequested);
    }

    [Fact]
    public async Task RunAsync_HelpRequested_WritesUsageWithoutConnectionString()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MiniBusToolingCliProgram.RunAsync(
            new[] { "--help" },
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("MiniBus local tooling", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_MissingConnectionString_ReturnsUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MiniBusToolingCliProgram.RunAsync(
            new[] { "inbox", "list" },
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required option: --connection-string <value>", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_OutboxDrainWithoutHostDispatcher_ReturnsUnsupportedExitCode()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await MiniBusToolingCliProgram.RunAsync(
            new[]
            {
                "--connection-string",
                "Server=localhost;Database=MiniBus;User Id=test;Password=test;",
                "outbox",
                "drain",
                "--max-batches",
                "1"
            },
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Standalone CLI drain requires", error.ToString(), StringComparison.Ordinal);
    }
}