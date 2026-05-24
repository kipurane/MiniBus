using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.Core.Contracts;

namespace MiniBus.Analyzers.Tests;

internal static class AnalyzerTestHost
{
    private static readonly MetadataReference[] References = CreateReferences();

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "MiniBusAnalyzerTests",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MiniBusUsageAnalyzer());
        var result = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();

        return result.OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal).ToImmutableArray();
    }

    private static MetadataReference[] CreateReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();

        var references = trustedPlatformAssemblies
            .Where(path => Path.GetFileName(path) is
                "System.Private.CoreLib.dll" or
                "System.Runtime.dll" or
                "System.Console.dll" or
                "System.Linq.dll" or
                "System.Collections.dll" or
                "System.Collections.Concurrent.dll" or
                "System.Private.Uri.dll" or
                "netstandard.dll")
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(ICommand).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(MiniBusProcessor).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(AzureServiceBusTransportRoutes).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(ServiceCollectionServiceExtensions).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(MiniBusAzureFunctionsServiceCollectionExtensions).Assembly.Location));

        return references.ToArray();
    }
}
