using Microsoft.CodeAnalysis;

namespace MiniBus.Analyzers.Tests;

public sealed class MiniBusUsageAnalyzerConfigurationTests
{
    [Fact]
    public async Task MiniBusProcessorUsageWithoutVisibleRegistrationProducesDiagnostic()
    {
        const string source = """
using System;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;

public static class Setup
{
    public static MiniBusProcessor Resolve(IServiceProvider services)
    {
        return services.GetRequiredService<MiniBusProcessor>();
    }
}
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MBAN007");
    }

    [Fact]
    public async Task MiniBusProcessorMemberUsageReportsMemberLocation()
    {
        const string source = """
using MiniBus.AzureFunctions.Processing;

public sealed class ProcessorConsumer
{
    private MiniBusProcessor? _processor;
}
""";

        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);
        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "MBAN007");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

        var sourceTree = diagnostic.Location.SourceTree!;
        var line = sourceTree.GetText().Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line].ToString();
        Assert.Contains("private MiniBusProcessor?", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VisibleAzureFunctionsRegistrationSuppressesProcessorDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions();
    }

    public static MiniBusProcessor Resolve(IServiceProvider services)
    {
        return services.GetRequiredService<MiniBusProcessor>();
    }
}
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN007");
    }

    [Fact]
    public async Task SagaUsageWithVisiblyDisabledSagasProducesDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options => options.EnableSagas = false);
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaUsageWithEnabledSagasDoesNotProduceDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options => options.EnableSagas = true);
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaTimeoutContractWithDefaultRegistrationDoesNotProduceDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions();
    }
}

public sealed record InvoiceTimeout(string InvoiceId) : ISagaTimeout;
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaTimeoutContractWithExplicitlyDisabledSagasProducesDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options => options.EnableSagas = false);
    }
}

public sealed record InvoiceTimeout(string InvoiceId) : ISagaTimeout;
""");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaUsageWithMixedVisibleRegistrationsDoesNotProduceDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions();
        services.AddMiniBusAzureFunctions(options => options.EnableSagas = true);
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaUsageWithFalseEnableSagasAndUnrelatedTrueProducesDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static bool OtherFlag => true;

    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options =>
        {
            options.EnableSagas = false;
            _ = OtherFlag == true;
        });
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task SagaUsageWithNonConstantEnableSagasDoesNotProduceDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public static class Setup
{
    public static bool SomeFlag { get; set; }

    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options => options.EnableSagas = SomeFlag);
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }

    [Fact]
    public async Task UnrelatedEnableSagasAssignmentDoesNotProduceSagaDiagnostic()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;
using MiniBus.Core.Sagas;

public sealed class OtherOptions
{
    public bool EnableSagas { get; set; }
}

public static class Setup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMiniBusAzureFunctions(options =>
        {
            new OtherOptions().EnableSagas = true;
        });
    }
}

public sealed record StartInvoice(string Id) : ICommand;

public sealed class InvoiceSagaData : ISagaData
{
    public string CorrelationId { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class InvoiceSaga : MiniBusSaga<InvoiceSagaData>, IHandleSagaMessages<StartInvoice>
{
    public override void ConfigureHowToFindSaga(SagaMapper<InvoiceSagaData> mapper)
    {
        mapper.StartsWith<StartInvoice>(message => message.Id);
    }

    public Task Handle(StartInvoice message, MiniBusContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
""");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "MBAN008");
    }
}
