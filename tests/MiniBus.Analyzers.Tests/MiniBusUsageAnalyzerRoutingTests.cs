using Microsoft.CodeAnalysis;

namespace MiniBus.Analyzers.Tests;

public sealed class MiniBusUsageAnalyzerRoutingTests
{
    [Fact]
    public async Task EmptyRouteDestinationProducesDiagnostic()
    {
        const string source = """
using MiniBus.AzureServiceBus.Routing;
using MiniBus.Core.Contracts;

public static class Setup
{
    public static void Configure(AzureServiceBusTransportRoutes routes)
    {
        routes.MapCommand<CreateInvoice>(" ");
    }
}

public sealed record CreateInvoice(string Id) : ICommand;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN005", "routes.MapCommand");
    }

    [Fact]
    public async Task NonCommandTypeInNonGenericMapCommandProducesDiagnostic()
    {
        const string source = """
using MiniBus.AzureServiceBus.Routing;
using MiniBus.Core.Contracts;

public static class Setup
{
    public static void Configure(AzureServiceBusTransportRoutes routes)
    {
        routes.MapCommand(typeof(InvoiceCreated), "events");
    }
}

public sealed record InvoiceCreated(string Id) : IEvent;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN004", "routes.MapCommand");
    }

    [Fact]
    public async Task MissingVisibleRouteProducesDiagnostic()
    {
        const string source = """
using System.Threading.Tasks;
using MiniBus.AzureServiceBus.Routing;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

public static class Setup
{
    public static void Configure(AzureServiceBusTransportRoutes routes)
    {
        routes.MapEvent<InvoiceCreated>("domain-events");
    }

    public static Task Send(MiniBusContext context)
    {
        return context.Send(new CreateInvoice("1"));
    }
}

public sealed record CreateInvoice(string Id) : ICommand;
public sealed record InvoiceCreated(string Id) : IEvent;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN006", "return context.Send");
    }

    [Fact]
    public async Task DynamicRouteConfigurationDoesNotProduceMissingRouteDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

public static class Setup
{
    public static Task Send(MiniBusContext context)
    {
        return context.Send(new CreateInvoice("1"));
    }
}

public sealed record CreateInvoice(string Id) : ICommand;
""");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ContextSendWithEventTypeProducesDiagnostic()
    {
        // MiniBusContext.Send<TCommand> has `where TCommand : ICommand`, so passing an IEvent
        // as an explicit type argument would be a constraint violation that Roslyn represents as
        // IInvalidOperation (skipped by the analyzer). To exercise AnalyzeContextContractMismatch,
        // we use an abstract subclass that shadows Send<T> without a constraint.
        const string source = """
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

public abstract class RelaxedContext : MiniBusContext
{
    public new abstract Task Send<T>(T message, CancellationToken cancellationToken = default);
}

public static class Setup
{
    public static Task Handle(RelaxedContext context)
    {
        return context.Send<InvoiceCreated>(new InvoiceCreated("1"));
    }
}

public sealed record InvoiceCreated(string Id) : IEvent;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN004", "context.Send<InvoiceCreated>");
    }

    [Fact]
    public async Task ContextPublishWithCommandTypeProducesDiagnostic()
    {
        // Same technique as ContextSendWithEventTypeProducesDiagnostic: shadow Publish<T>
        // on a subclass to remove the constraint so the type argument is analyzable.
        const string source = """
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

public abstract class RelaxedContext : MiniBusContext
{
    public new abstract Task Publish<T>(T message, CancellationToken cancellationToken = default);
}

public static class Setup
{
    public static Task Handle(RelaxedContext context)
    {
        return context.Publish<CreateInvoice>(new CreateInvoice("1"));
    }
}

public sealed record CreateInvoice(string Id) : ICommand;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN004", "context.Publish<CreateInvoice>");
    }

    [Fact]
    public async Task ContextSendAndPublishWithCorrectTypesProduceNoDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

public static class Setup
{
    public static async Task Handle(MiniBusContext context)
    {
        await context.Send(new CreateInvoice("1"));
        await context.Publish(new InvoiceCreated("1"));
    }
}

public sealed record CreateInvoice(string Id) : ICommand;
public sealed record InvoiceCreated(string Id) : IEvent;
""");

        Assert.Empty(diagnostics);
    }

    private static void AssertSingleWarning(
        IReadOnlyCollection<Diagnostic> diagnostics,
        string expectedId,
        string expectedLineText)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

        Assert.NotNull(diagnostic.Location.SourceTree);
        var sourceTree = diagnostic.Location.SourceTree!;
        var line = sourceTree.GetText().Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line].ToString();
        Assert.Contains(expectedLineText, line, StringComparison.Ordinal);
    }
}
