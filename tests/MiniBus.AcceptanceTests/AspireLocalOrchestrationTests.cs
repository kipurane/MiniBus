using System.Text.RegularExpressions;

namespace MiniBus.AcceptanceTests;

public sealed class AspireLocalOrchestrationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void AppHostReferencesExpectedProjects()
    {
        var project = Read("samples/MiniBus.Samples.AppHost/MiniBus.Samples.AppHost.csproj");

        Assert.Contains(@"Aspire.AppHost.Sdk", project, StringComparison.Ordinal);
        Assert.Contains("<IsAspireHost>true</IsAspireHost>", project, StringComparison.Ordinal);
        Assert.Contains("Aspire.Hosting.AppHost", project, StringComparison.Ordinal);
        Assert.Contains(@"..\MiniBus.Samples.Billing.FunctionApp\MiniBus.Samples.Billing.FunctionApp.csproj", project, StringComparison.Ordinal);
        Assert.Contains(@"..\MiniBus.Samples.Inventory.FunctionApp\MiniBus.Samples.Inventory.FunctionApp.csproj", project, StringComparison.Ordinal);
        Assert.Contains(@"..\MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp\MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp.csproj", project, StringComparison.Ordinal);
        Assert.Contains(@"..\..\src\MiniBus.Tooling.Web\MiniBus.Tooling.Web.csproj", project, StringComparison.Ordinal);
    }

    [Fact]
    public void AppHostDefinesStableResourcesAndSharedSettings()
    {
        var program = Read("samples/MiniBus.Samples.AppHost/Program.cs");

        Assert.Contains("billing-functionapp", program, StringComparison.Ordinal);
        Assert.Contains("inventory-functionapp", program, StringComparison.Ordinal);
        Assert.Contains("billing-outbox-dispatcher", program, StringComparison.Ordinal);
        Assert.Contains("billing-sql-schema-apply", program, StringComparison.Ordinal);
        Assert.Contains("minibus-tooling-web", program, StringComparison.Ordinal);
        Assert.Contains("servicebus-emulator", program, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/mssql/server", program, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/azure-storage/azurite", program, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/azure-messaging/servicebus-emulator", program, StringComparison.Ordinal);
        Assert.Contains("minibus-aspire-billing-servicebus-emulator", program, StringComparison.Ordinal);
        Assert.Contains("isProxied: false", program, StringComparison.Ordinal);
        Assert.Contains(@".WithArgs(""--port"", ""7071"")", program, StringComparison.Ordinal);
        Assert.Contains(@".WithArgs(""--port"", ""7072"")", program, StringComparison.Ordinal);
        Assert.Contains(@".WithArgs(""--port"", ""7073"")", program, StringComparison.Ordinal);
        Assert.Contains("--apply-sql-schema", program, StringComparison.Ordinal);
        Assert.Contains("WaitForCompletion(billingSqlSchemaApplier)", program, StringComparison.Ordinal);
        Assert.Contains("Config.json", program, StringComparison.Ordinal);
        Assert.Contains("accept-eula", program, StringComparison.Ordinal);
        Assert.Contains("GetEulaAcceptance", program, StringComparison.Ordinal);
        Assert.Contains("BillingSqlEnabled", program, StringComparison.Ordinal);
        Assert.Contains("MiniBus__Tooling__Sql__ConnectionString", program, StringComparison.Ordinal);
        Assert.Contains("MiniBus__Tooling__Sql__SchemaName", program, StringComparison.Ordinal);
    }

    [Fact]
    public void AppHostDocumentsExplicitSchemaSetupAndManualFallback()
    {
        var readme = Read("samples/MiniBus.Samples.AppHost/README.md");
        var launchSettings = Read("samples/MiniBus.Samples.AppHost/Properties/launchSettings.json");

        Assert.Contains("billing-sql-schema-apply", readme, StringComparison.Ordinal);
        Assert.Contains("idempotent MiniBus SQL schema scripts", readme, StringComparison.Ordinal);
        Assert.Contains("ACCEPT_EULA=Y", readme, StringComparison.Ordinal);
        Assert.Contains("7073", readme, StringComparison.Ordinal);
        Assert.Contains(@"""ACCEPT_EULA"": ""Y""", launchSettings, StringComparison.Ordinal);
        Assert.Contains("docker compose down", readme, StringComparison.Ordinal);
        Assert.Contains("Manual Fallback", readme, StringComparison.Ordinal);
        Assert.Contains("MiniBus.Tooling.Web", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void AspireHostingDependencyIsIsolatedToAppHost()
    {
        var projectFiles = Directory.EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(RepositoryRoot, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .ToArray();

        var offendingFiles = projectFiles
            .Where(path => !Path.GetRelativePath(RepositoryRoot, path).Equals(
                "samples/MiniBus.Samples.AppHost/MiniBus.Samples.AppHost.csproj",
                StringComparison.Ordinal))
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"<PackageReference\s+Include=""Aspire\.", RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(offendingFiles);
    }

    private static string Read(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MiniBus.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the MiniBus repository root.");
    }
}
