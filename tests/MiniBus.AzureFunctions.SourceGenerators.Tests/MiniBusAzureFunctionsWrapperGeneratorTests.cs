using System.Collections.Immutable;
using System.Text.Json;
using System.Xml.Linq;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.SourceGenerators;

namespace MiniBus.AzureFunctions.SourceGenerators.Tests;

public sealed class MiniBusAzureFunctionsWrapperGeneratorTests
{
    [Fact]
    public void QueueDeclarationGeneratesThinWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: "billing-queue",
    connection: "ServiceBus")]
""");

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
        Assert.Contains("namespace MiniBus.AzureFunctions.__Generated", generated.Source);
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.Function(\"BillingInput\")]", generated.Source);
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(\"billing-queue\", Connection = \"ServiceBus\")]", generated.Source);
        Assert.Contains("global::Azure.Messaging.ServiceBus.ServiceBusReceivedMessage message", generated.Source);
        Assert.Contains("global::Microsoft.Azure.Functions.Worker.ServiceBusMessageActions actions", generated.Source);
        Assert.Contains("return _processor.ProcessAsync(message, actions, cancellationToken);", generated.Source);
    }

    [Fact]
    public void TopicDeclarationGeneratesThinWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusTopicFunction(
    functionName: "BillingEvents",
    topicName: "domain-events",
    subscriptionName: "billing",
    connection: "ServiceBus")]
""");

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.Function(\"BillingEvents\")]", generated.Source);
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(\"domain-events\", \"billing\", Connection = \"ServiceBus\")]", generated.Source);
        Assert.Contains("return _processor.ProcessAsync(message, actions, cancellationToken);", generated.Source);
    }

    [Fact]
    public void AliasDeclarationGeneratesThinWrapper()
    {
        var result = RunGenerator("""
using Q = MiniBus.AzureFunctions.SourceGenerators.Declarations.MiniBusSourceGeneratedServiceBusQueueFunctionAttribute;

[assembly: Q(
    functionName: "BillingInput",
    queueName: "billing-queue",
    connection: "ServiceBus")]
""");

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.Function(\"BillingInput\")]", generated.Source);
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(\"billing-queue\", Connection = \"ServiceBus\")]", generated.Source);
    }

    [Fact]
    public void PropertyAssignmentDeclarationGeneratesThinWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    FunctionName = "BillingInput",
    QueueName = "billing-queue",
    Connection = "ServiceBus")]
""");

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.Function(\"BillingInput\")]", generated.Source);
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(\"billing-queue\", Connection = \"ServiceBus\")]", generated.Source);
    }

    [Fact]
    public void EmptyQueueDeclarationValuesProduceDiagnosticsAndNoWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "",
    queueName: "",
    connection: "")]
""");

        Assert.Equal(new[] { "MBFWR001", "MBFWR001", "MBFWR001" }, result.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray());
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void NonConstantQueueDeclarationValueProducesDiagnosticAndNoWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: System.Environment.MachineName,
    connection: "ServiceBus")]
""");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("MBFWR003", diagnostic.Id);
        Assert.Contains("compile-time constant string", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ConstQueueDeclarationValueGeneratesThinWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: Constants.QueueName,
    connection: "ServiceBus")]

internal static class Constants
{
    public const string QueueName = "billing-queue";
}
""");

        Assert.Empty(result.Diagnostics);
        var generated = Assert.Single(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
        Assert.Contains("[global::Microsoft.Azure.Functions.Worker.ServiceBusTrigger(\"billing-queue\", Connection = \"ServiceBus\")]", generated.Source);
    }

    [Fact]
    public void EmptyTopicDeclarationValuesProduceDiagnosticsAndNoWrapper()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusTopicFunction(
    functionName: "",
    topicName: "",
    subscriptionName: "",
    connection: "")]
""");

        Assert.Equal(new[] { "MBFWR001", "MBFWR001", "MBFWR001", "MBFWR001" }, result.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray());
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateFunctionNamesProduceDiagnosticsAndNoDuplicateWrappers()
    {
        var result = RunGenerator("""
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction("BillingInput", "billing-queue", "ServiceBus")]
[assembly: MiniBusSourceGeneratedServiceBusTopicFunction("BillingInput", "domain-events", "billing", "ServiceBus")]
""");

        Assert.Equal(new[] { "MBFWR002", "MBFWR002" }, result.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray());
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName.EndsWith("Function.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratedOutputIsDeterministic()
    {
        const string Source = """
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction("BillingInput", "billing-queue", "ServiceBus")]
[assembly: MiniBusSourceGeneratedServiceBusTopicFunction("BillingEvents", "domain-events", "billing", "ServiceBus")]
""";

        var first = RunGenerator(Source);
        var second = RunGenerator(Source);

        Assert.Equal(
            first.GeneratedSources.Select(source => (source.HintName, source.Source)).OrderBy(source => source.HintName),
            second.GeneratedSources.Select(source => (source.HintName, source.Source)).OrderBy(source => source.HintName));
    }

    [Fact]
    public void RuntimePackagesDoNotReferenceRoslynPackages()
    {
        var runtimeProjectFiles = GetRuntimeProjectFiles().ToArray();

        Assert.NotEmpty(runtimeProjectFiles);
        foreach (var projectFile in runtimeProjectFiles)
        {
            var packageReferences = GetPackageReferenceNames(projectFile);
            Assert.DoesNotContain(packageReferences, IsRoslynPackageName);
        }
    }

    [Fact]
    public void RuntimePackageAssetsDoNotResolveRoslynPackages()
    {
        var runtimeProjectFiles = GetRuntimeProjectFiles().ToArray();

        Assert.NotEmpty(runtimeProjectFiles);
        foreach (var projectFile in runtimeProjectFiles)
        {
            var packageNames = GetResolvedPackageNames(projectFile);
            Assert.DoesNotContain(packageNames, IsRoslynPackageName);
        }
    }

    private static IEnumerable<string> GetRuntimeProjectFiles()
    {
        return Directory.EnumerateFiles(GetRepositoryRoot(), "*.csproj", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("MiniBus.AzureFunctions.SourceGenerators.csproj", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> GetPackageReferenceNames(string projectFile)
    {
        var project = XDocument.Load(projectFile);
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> GetResolvedPackageNames(string projectFile)
    {
        var assetsPath = Path.Combine(Path.GetDirectoryName(projectFile)!, "obj", "project.assets.json");
        Assert.True(File.Exists(assetsPath), $"Expected assets file to exist for {projectFile}.");

        using var stream = File.OpenRead(assetsPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("libraries", out var libraries))
        {
            return [];
        }

        return libraries
            .EnumerateObject()
            .Where(property => property.Value.TryGetProperty("type", out var type) && type.GetString() == "package")
            .Select(property => property.Name.Split('/')[0])
            .ToArray();
    }

    private static bool IsRoslynPackageName(string packageName)
    {
        return packageName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase);
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13);
        var compilation = CSharpCompilation.Create(
            assemblyName: "MiniBusGeneratorTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MiniBusAzureFunctionsWrapperGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);

        var result = Assert.Single(driver.GetRunResult().Results);
        return new GeneratorRunResult(
            result.Diagnostics,
            result.GeneratedSources.Select(source => new GeneratedSource(source.HintName, source.SourceText.ToString())).ToArray());
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Task).Assembly,
            typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly,
            typeof(ServiceBusReceivedMessage).Assembly,
            typeof(FunctionAttribute).Assembly,
            typeof(ServiceBusMessageActions).Assembly,
            typeof(MiniBusProcessor).Assembly
        };

        return assemblies
            .Distinct()
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MiniBus.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed record GeneratedSource(string HintName, string Source);

    private sealed record GeneratorRunResult(ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<GeneratedSource> GeneratedSources);
}
