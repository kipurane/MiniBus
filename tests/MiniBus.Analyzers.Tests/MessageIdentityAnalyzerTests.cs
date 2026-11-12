namespace MiniBus.Analyzers.Tests;

public sealed class MessageIdentityAnalyzerTests
{
    [Fact]
    public async Task ReportsBlankAliasAndRepeatedAliasAtTheirLocations()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities) {
    identities.Map<A>("primary", "", "old", "old");
  }
}
""");
        var blank = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN009");
        var duplicate = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN010");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, blank.Severity);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, duplicate.Severity);
        Assert.Equal("\"\"", blank.Location.SourceTree!.GetText().ToString(blank.Location.SourceSpan));
        Assert.Equal("\"old\"", duplicate.Location.SourceTree!.GetText().ToString(duplicate.Location.SourceSpan));
    }

    [Fact]
    public async Task DoesNotInferConflictsAcrossBranchesOrDynamicValues()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities, bool choice, string name) {
    if (choice) { identities.Map<A>("same"); }
    else { identities.Map<B>("same"); }
    identities.Map<A>(name);
    identities.Map<B>("different");
  }
}
""");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "MBAN009" or "MBAN010");
    }
    [Fact]
    public async Task AnalyzesOptionsRegistrationsAndKeepsInstancesSeparate()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.AzureFunctions.Processing;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusProcessorOptions first, MiniBusProcessorOptions second, string dynamicName) {
    first.MessageIdentities.Map<A>(" ");
    first.MessageIdentities.Map<A>("one", "old");
    second.MessageIdentities.Map<B>("one");
    first.MessageIdentities.Map<B>("old");
    first.MessageIdentities.Map<A>("two");
    second.MessageIdentities.Map<A>(dynamicName);
  }
}
""");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN009");
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == "MBAN010"));
    }

    [Fact]
    public async Task DoesNotAssumeRegistryIdentityAfterOptionsReassignment()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.AzureFunctions.Processing;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusProcessorOptions options) {
    options.MessageIdentities.Map<A>("same");
    options = new MiniBusProcessorOptions();
    options.MessageIdentities.Map<B>("same");
  }
}
""");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN010");
    }
    [Fact]
    public async Task HandlesNamedArgumentsWithoutConfusingAliasesAndPrimary()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities) {
    identities.Map<A>(aliases: new[] { "old" }, identity: "primary");
    identities.Map(messageType: typeof(B), identity: "old");
  }
}
""");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN010");
    }

    [Fact]
    public async Task DoesNotMergeDifferentRegistryInstancesThroughPropertySymbol()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Holder { public MiniBusMessageIdentityRegistry Registry { get; } = new(); }
public class Setup {
  public void Configure(Holder first, Holder second) {
    first.Registry.Map<A>("same");
    second.Registry.Map<B>("same");
  }
}
""");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN010");
    }
    [Fact]
    public async Task AnalyzesExplicitAliasArraysAndTypeofRegistrations()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities) {
    identities.Map(typeof(A), "first", new[] { "old", " " });
    identities.Map<B>("old");
  }
}
""");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN009");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN010");
    }
    [Fact]
    public async Task ReportsBlankAndConflictingRegistrations()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities) {
    identities.Map<A>(" ");
    identities.Map<A>("first", "old");
    identities.Map<B>("old");
    identities.Map<A>("second");
  }
}
""");
        Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN009");
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == "MBAN010"));
    }

    [Fact]
    public async Task IgnoresDynamicAndDistinctRegistrations()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
public class A : ICommand { }
public class B : ICommand { }
public class Setup {
  public void Configure(MiniBusMessageIdentityRegistry identities, string dynamicName) {
    identities.Map<A>("name", "old");
    identities.Map<B>("Name", dynamicName);
  }
}
""");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "MBAN009" or "MBAN010");
    }
}
