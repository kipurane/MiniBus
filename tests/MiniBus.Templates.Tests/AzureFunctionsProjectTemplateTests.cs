using System.Text.Json;

namespace MiniBus.Templates.Tests;

public sealed class AzureFunctionsProjectTemplateTests
{
    [Fact]
    public void TemplateMetadataExposesFocusedAzureFunctionsStarter()
    {
        using var templateJson = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(GetTemplateRoot(), ".template.config", "template.json")));
        var metadata = templateJson.RootElement;

        Assert.Equal("minibus-functionapp", metadata.GetProperty("shortName").GetString());
        Assert.Equal("MiniBus.FunctionApp.Template", metadata.GetProperty("sourceName").GetString());
        Assert.False(metadata.TryGetProperty("symbols", out _));
    }

    [Fact]
    public void TemplateSourceContainsStarterHostAndConfiguration()
    {
        var templateRoot = GetTemplateRoot();

        Assert.True(File.Exists(Path.Combine(templateRoot, "Program.cs")));
        Assert.True(File.Exists(Path.Combine(templateRoot, "host.json")));

        var wrapper = File.ReadAllText(Path.Combine(templateRoot, "StarterInputFunction.cs"));
        Assert.Contains("ServiceBusTrigger", wrapper, StringComparison.Ordinal);
        Assert.Contains("_processor.ProcessAsync(message, actions, cancellationToken)", wrapper, StringComparison.Ordinal);

        var settings = File.ReadAllText(Path.Combine(templateRoot, "local.settings.json"));
        Assert.Contains("\"ServiceBus\"", settings, StringComparison.Ordinal);

        var readme = File.ReadAllText(Path.Combine(templateRoot, "README.md"));
        Assert.Contains("starter-commands", readme, StringComparison.Ordinal);
        Assert.Contains("UseDevelopmentEmulator=true", readme, StringComparison.Ordinal);
        Assert.Contains("source-generated wrappers", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SQL inbox/outbox/saga persistence", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateSourceKeepsV1DependenciesFocused()
    {
        var projectFile = File.ReadAllText(
            Path.Combine(GetTemplateRoot(), "MiniBus.FunctionApp.Template.csproj"));

        Assert.Contains("MiniBus.Core", projectFile, StringComparison.Ordinal);
        Assert.Contains("MiniBus.AzureFunctions", projectFile, StringComparison.Ordinal);
        Assert.Contains("MiniBus.AzureServiceBus", projectFile, StringComparison.Ordinal);
        Assert.Contains("MiniBus.Analyzers", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("MiniBus.AzureFunctions.SourceGenerators", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("MiniBus.Persistence.Sql", projectFile, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(GetTemplateRoot(), "Sagas")));
    }

    private static string GetTemplateRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "TemplateContent");
    }
}
