using Microsoft.CodeAnalysis;

namespace MiniBus.Analyzers.Tests;

public sealed class MiniBusUsageAnalyzerHandlerTests
{
    [Fact]
    public async Task AbstractHandlerProducesDiagnostic()
    {
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;

public sealed record CreateInvoice(string Id) : ICommand;

public abstract class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    public abstract Task Handle(CreateInvoice message, MiniBusContext context, CancellationToken cancellationToken);
}
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN001", "public abstract class CreateInvoiceHandler");
    }

    [Fact]
    public async Task OpenGenericHandlerTypeWithoutVisibleRegistrationDoesNotProduceDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;

public sealed record CreateInvoice(string Id) : ICommand;

public sealed class GenericHandler<TMessage> : IHandleMessages<TMessage>
    where TMessage : IMessage
{
    public Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task OpenGenericHandlerRegistrationProducesDiagnostic()
    {
        const string source = """
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;

public sealed class GenericHandler<TMessage> : IHandleMessages<TMessage>
    where TMessage : IMessage
{
    public Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddTransient(typeof(IHandleMessages<>), typeof(GenericHandler<>));
    }
}
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN002", "services.AddTransient");
    }

    [Fact]
    public async Task UnrelatedAddMethodWithTypeofArgumentsDoesNotProduceOpenGenericHandlerDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System;
using System.Threading;
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;

public sealed class GenericHandler<TMessage> : IHandleMessages<TMessage>
    where TMessage : IMessage
{
    public Task Handle(TMessage message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class Setup
{
    public static void Configure()
    {
        AddWhatever(typeof(IHandleMessages<>), typeof(GenericHandler<>));
    }

    private static void AddWhatever(Type serviceType, Type implementationType)
    {
    }
}
""");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidConcreteHandlerDoesNotProduceHandlerDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Handlers;

public sealed record CreateInvoice(string Id) : ICommand;

public sealed class CreateInvoiceHandler : IHandleMessages<CreateInvoice>
{
    public Task Handle(CreateInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AmbiguousMessageContractProducesDiagnostic()
    {
        const string source = """
using MiniBus.Core.Contracts;

public sealed record AmbiguousMessage(string Id) : ICommand, IEvent;
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        AssertSingleWarning(diagnostics, "MBAN003", "public sealed record AmbiguousMessage");
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
        var lineSpan = diagnostic.Location.GetLineSpan();
        var line = sourceTree.GetText().Lines[lineSpan.StartLinePosition.Line].ToString();
        Assert.Contains(expectedLineText, line, StringComparison.Ordinal);
    }
}
