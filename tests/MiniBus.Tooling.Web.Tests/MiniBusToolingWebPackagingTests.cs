using System.Text.Json;

namespace MiniBus.Tooling.Web.Tests;

public sealed class MiniBusToolingWebPackagingTests
{
    [Fact]
    public void StaticAssets_ArePresentForPackagedWebApp()
    {
        var projectRoot = FindProjectRoot();

        Assert.True(File.Exists(Path.Combine(projectRoot, "wwwroot", "index.html")));
        Assert.True(File.Exists(Path.Combine(projectRoot, "wwwroot", "assets", "index.js")));
        Assert.True(File.Exists(Path.Combine(projectRoot, "wwwroot", "assets", "index.css")));
    }

    [Fact]
    public void ClientPackage_DefinesReactTypescriptBuild()
    {
        var packageJson = File.ReadAllText(Path.Combine(FindProjectRoot(), "ClientApp", "package.json"));
        using var document = JsonDocument.Parse(packageJson);
        var root = document.RootElement;

        Assert.Equal("minibus-tooling-web", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("dependencies").TryGetProperty("react", out _));
        Assert.True(root.GetProperty("dependencies").TryGetProperty("typescript", out _));
        Assert.Contains("vite build", root.GetProperty("scripts").GetProperty("build").GetString(), StringComparison.Ordinal);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MiniBus.Tooling.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/MiniBus.Tooling.Web.");
    }
}
