using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MiniBus.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MiniBusUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            MiniBusDiagnosticDescriptors.AbstractHandler,
            MiniBusDiagnosticDescriptors.OpenGenericHandler,
            MiniBusDiagnosticDescriptors.AmbiguousMessageContract,
            MiniBusDiagnosticDescriptors.MessageContractMismatch,
            MiniBusDiagnosticDescriptors.EmptyRouteDestination,
            MiniBusDiagnosticDescriptors.MissingVisibleRoute,
            MiniBusDiagnosticDescriptors.MissingAzureFunctionsRegistration,
            MiniBusDiagnosticDescriptors.SagaProcessingDisabled);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var symbols = MiniBusSymbols.Create(context.Compilation);
        if (symbols.IMessage is null)
        {
            return;
        }

        var state = new AnalyzerState();

        context.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, symbols, state), SymbolKind.NamedType);
        context.RegisterOperationAction(operationContext => AnalyzeInvocation(operationContext, symbols, state), OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(syntaxContext => AnalyzeInvocationSyntax(syntaxContext, symbols), SyntaxKind.InvocationExpression);
        context.RegisterCompilationEndAction(compilationContext => AnalyzeCompilationEnd(compilationContext, state));
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.Implements(symbols.ICommand) && type.Implements(symbols.IEvent))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MiniBusDiagnosticDescriptors.AmbiguousMessageContract,
                type.Locations.FirstOrDefault(),
                type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }

        if (type.ImplementsGeneric(symbols.IHandleMessages))
        {
            if (type.IsAbstract)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MiniBusDiagnosticDescriptors.AbstractHandler,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            }

        }

        if (type.ImplementsGeneric(symbols.IHandleSagaMessages)
            || type.InheritsFromGeneric(symbols.MiniBusSaga)
            || type.Implements(symbols.ISagaTimeout))
        {
            if (type.Locations.FirstOrDefault() is { } location)
            {
                state.SagaUsageLocations.Enqueue(location);
            }
        }

        EnqueueMiniBusProcessorMemberUsageLocations(type, symbols.MiniBusProcessor, state);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod;

        AnalyzeHandlerRegistration(context, symbols, state, invocation, target);
        AnalyzeRouteInvocation(context, symbols, state, invocation, target);
        AnalyzeContextInvocation(context, symbols, state, invocation, target);
        AnalyzeAzureFunctionsInvocation(context, symbols, state, invocation, target);
    }

    private static void AnalyzeInvocationSyntax(
        SyntaxNodeAnalysisContext context,
        MiniBusSymbols symbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        var method = symbolInfo.Symbol as IMethodSymbol
            ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (!IsKnownDependencyInjectionRegistrationMethod(method)
            || invocation.ArgumentList.Arguments.Count < 2
            || invocation.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax serviceTypeOf
            || invocation.ArgumentList.Arguments[1].Expression is not TypeOfExpressionSyntax implementationTypeOf)
        {
            return;
        }

        var serviceType = GetTypeOfTypeSymbol(context, serviceTypeOf);
        var implementationType = GetTypeOfTypeSymbol(context, implementationTypeOf);
        if (serviceType is null
            || implementationType is null
            || !IsHandleMessagesServiceType(serviceType, symbols.IHandleMessages)
            || !IsOpenGenericType(implementationType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MiniBusDiagnosticDescriptors.OpenGenericHandler,
            invocation.GetLocation(),
            implementationType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static void AnalyzeHandlerRegistration(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state,
        IInvocationOperation invocation,
        IMethodSymbol target)
    {
        if (!IsKnownDependencyInjectionRegistrationMethod(target))
        {
            return;
        }

        var serviceType = GetRegistrationTypeArgument(invocation, target, 0);
        var implementationType = GetRegistrationTypeArgument(invocation, target, 1);
        if (serviceType is null || implementationType is null)
        {
            return;
        }

        if (!IsHandleMessagesServiceType(serviceType, symbols.IHandleMessages))
        {
            return;
        }

        if (target.TypeArguments.Length >= 2 && IsOpenGenericType(implementationType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MiniBusDiagnosticDescriptors.OpenGenericHandler,
                invocation.Syntax.GetLocation(),
                implementationType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }
    }

    private static void AnalyzeRouteInvocation(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state,
        IInvocationOperation invocation,
        IMethodSymbol target)
    {
        if (target.ContainingType is null
            || !SymbolEqualityComparer.Default.Equals(target.ContainingType, symbols.AzureServiceBusTransportRoutes))
        {
            return;
        }

        var routeKind = target.Name switch
        {
            "MapCommand" => RouteKind.Command,
            "MapEvent" => RouteKind.Event,
            "MapScheduledMessage" => RouteKind.Scheduled,
            _ => RouteKind.None
        };

        if (routeKind == RouteKind.None)
        {
            return;
        }

        var messageType = GetRouteMessageType(invocation, target);
        if (messageType is not null)
        {
            state.AddRoute(routeKind, messageType);
            AnalyzeRouteContractMismatch(context, symbols, invocation, target, routeKind, messageType);
        }

        var destination = GetConstantRouteDestination(invocation);
        if (destination is not null && string.IsNullOrWhiteSpace(destination))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MiniBusDiagnosticDescriptors.EmptyRouteDestination,
                invocation.Syntax.GetLocation(),
                target.Name,
                messageType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "unknown message"));
        }
    }

    private static void AnalyzeContextInvocation(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state,
        IInvocationOperation invocation,
        IMethodSymbol target)
    {
        if (target.ContainingType is null
            || !target.ContainingType.IsOrInheritsFrom(symbols.MiniBusContext))
        {
            return;
        }

        var routeKind = target.Name switch
        {
            "Send" => RouteKind.Command,
            "Publish" => RouteKind.Event,
            "Schedule" => RouteKind.Scheduled,
            _ => RouteKind.None
        };

        if (routeKind == RouteKind.None)
        {
            return;
        }

        var messageType = target.TypeArguments.FirstOrDefault();
        if (messageType is null)
        {
            return;
        }

        AnalyzeContextContractMismatch(context, symbols, invocation, target.Name, routeKind, messageType);
        state.AddUsage(routeKind, messageType, invocation.Syntax.GetLocation());
    }

    private static void AnalyzeAzureFunctionsInvocation(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        AnalyzerState state,
        IInvocationOperation invocation,
        IMethodSymbol target)
    {
        var effectiveTarget = target.ReducedFrom ?? target;

        if (effectiveTarget.Name == "AddMiniBusAzureFunctions"
            && SymbolEqualityComparer.Default.Equals(effectiveTarget.ContainingType, symbols.MiniBusAzureFunctionsServiceCollectionExtensions))
        {
            state.AzureFunctionsRegistrationVisible = true;

            var sagaRegistrationState = GetSagaRegistrationState(invocation, symbols);
            if (sagaRegistrationState == SagaRegistrationState.Enabled)
            {
                state.SagasEnabledVisible = true;
            }
            else if (sagaRegistrationState == SagaRegistrationState.Disabled)
            {
                state.SagaDisabledRegistrationLocations.Enqueue(invocation.Syntax.GetLocation());
            }
        }

        var typeArguments = effectiveTarget.TypeArguments;
        if (effectiveTarget.Name is "GetRequiredService" or "GetService"
            && typeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(typeArguments[0], symbols.MiniBusProcessor))
        {
            state.MiniBusProcessorUsageLocations.Enqueue(invocation.Syntax.GetLocation());
        }
    }

    private static void AnalyzeCompilationEnd(CompilationAnalysisContext context, AnalyzerState state)
    {
        if (state.AnyRoutesVisible)
        {
            foreach (var usage in state.MessageUsages)
            {
                if (!state.HasRouteFor(usage.Kind, usage.MessageType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MiniBusDiagnosticDescriptors.MissingVisibleRoute,
                        usage.Location,
                        usage.Kind.ToDiagnosticText(),
                        usage.MessageType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }
            }
        }

        if (!state.AzureFunctionsRegistrationVisible)
        {
            foreach (var location in GetDistinctLocations(state.MiniBusProcessorUsageLocations))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MiniBusDiagnosticDescriptors.MissingAzureFunctionsRegistration,
                    location));
            }
        }

        if (!state.SagaUsageLocations.IsEmpty && !state.SagasEnabledVisible)
        {
            foreach (var location in state.SagaDisabledRegistrationLocations)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MiniBusDiagnosticDescriptors.SagaProcessingDisabled,
                    location));
            }
        }
    }

    private static void AnalyzeRouteContractMismatch(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        IInvocationOperation invocation,
        IMethodSymbol target,
        RouteKind routeKind,
        ITypeSymbol messageType)
    {
        var expectedContract = routeKind switch
        {
            RouteKind.Command => symbols.ICommand,
            RouteKind.Event => symbols.IEvent,
            _ => symbols.IMessage
        };

        if (expectedContract is null)
        {
            return;
        }

        if (!messageType.Implements(expectedContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MiniBusDiagnosticDescriptors.MessageContractMismatch,
                invocation.Syntax.GetLocation(),
                target.Name,
                messageType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                expectedContract.Name));
        }
    }

    private static void AnalyzeContextContractMismatch(
        OperationAnalysisContext context,
        MiniBusSymbols symbols,
        IInvocationOperation invocation,
        string operationName,
        RouteKind routeKind,
        ITypeSymbol messageType)
    {
        var expectedContract = routeKind switch
        {
            RouteKind.Command => symbols.ICommand,
            RouteKind.Event => symbols.IEvent,
            _ => symbols.IMessage
        };

        if (expectedContract is null)
        {
            return;
        }

        if (!messageType.Implements(expectedContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MiniBusDiagnosticDescriptors.MessageContractMismatch,
                invocation.Syntax.GetLocation(),
                operationName,
                messageType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                expectedContract.Name));
        }
    }

    private static ITypeSymbol? GetRouteMessageType(IInvocationOperation invocation, IMethodSymbol target)
    {
        if (target.TypeArguments.Length == 1)
        {
            return target.TypeArguments[0];
        }

        if (invocation.Arguments.Length == 0)
        {
            return null;
        }

        return invocation.Arguments[0].Value is ITypeOfOperation typeOfOperation
            ? typeOfOperation.TypeOperand
            : null;
    }

    private static INamedTypeSymbol? GetRegistrationTypeArgument(
        IInvocationOperation invocation,
        IMethodSymbol target,
        int index)
    {
        if (target.TypeArguments.Length > index)
        {
            return target.TypeArguments[index] as INamedTypeSymbol;
        }

        if (invocation.Arguments.Length <= index)
        {
            return null;
        }

        return invocation.Arguments[index].Value is ITypeOfOperation typeOfOperation
            ? typeOfOperation.TypeOperand as INamedTypeSymbol
            : null;
    }

    private static bool IsHandleMessagesServiceType(
        INamedTypeSymbol serviceType,
        INamedTypeSymbol? handleMessages)
    {
        if (serviceType.ImplementsGeneric(handleMessages)
            || SymbolEqualityComparer.Default.Equals(serviceType.OriginalDefinition, handleMessages))
        {
            return true;
        }

        return serviceType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == "global::MiniBus.Core.Handlers.IHandleMessages<>";
    }

    private static bool IsKnownDependencyInjectionRegistrationMethod(IMethodSymbol? method)
    {
        var effectiveMethod = method?.ReducedFrom ?? method;
        if (effectiveMethod is null)
        {
            return false;
        }

        if (effectiveMethod.Name is not ("AddTransient" or "AddScoped" or "AddSingleton"))
        {
            return false;
        }

        var containingNamespace = effectiveMethod.ContainingNamespace.ToDisplayString();
        if (!containingNamespace.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
        {
            return false;
        }

        return method?.ReducedFrom is not null
            || (effectiveMethod.Parameters.Length > 0
                && string.Equals(
                    effectiveMethod.Parameters[0].Type.ToDisplayString(),
                    "Microsoft.Extensions.DependencyInjection.IServiceCollection",
                    StringComparison.Ordinal));
    }

    private static INamedTypeSymbol? GetTypeOfTypeSymbol(
        SyntaxNodeAnalysisContext context,
        TypeOfExpressionSyntax typeOfExpression)
    {
        var typeInfo = context.SemanticModel.GetTypeInfo(typeOfExpression.Type, context.CancellationToken);
        return (typeInfo.Type ?? typeInfo.ConvertedType) as INamedTypeSymbol;
    }

    private static bool IsOpenGenericType(INamedTypeSymbol type)
    {
        return (type.IsGenericType || type.IsUnboundGenericType)
            && (type.IsUnboundGenericType
                || type.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter));
    }

    private static string? GetConstantRouteDestination(IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (!IsRouteDestinationParameter(argument.Parameter))
            {
                continue;
            }

            var value = argument.Value.ConstantValue;
            return value.HasValue ? value.Value as string : null;
        }

        return null;
    }

    private static bool IsRouteDestinationParameter(IParameterSymbol? parameter)
    {
        return parameter?.Name is "queue" or "topic" or "destination" or "queueOrTopicName";
    }

    private static SagaRegistrationState GetSagaRegistrationState(IInvocationOperation invocation, MiniBusSymbols symbols)
    {
        if (invocation.Arguments.Length == 0)
        {
            return SagaRegistrationState.Unknown;
        }

        var sawDisabledEnableSagasAssignment = false;
        var sawUnknownEnableSagasAssignment = false;
        foreach (var operation in EnumerateOperations(invocation))
        {
            if (operation is not IAssignmentOperation assignment
                || assignment.Target is not IPropertyReferenceOperation propertyReference
                || propertyReference.Property.Name != "EnableSagas"
                || !IsMiniBusProcessorOptionsProperty(propertyReference, symbols))
            {
                continue;
            }

            var constantValue = assignment.Value.ConstantValue;
            if (constantValue.HasValue && constantValue.Value is bool value)
            {
                if (value)
                {
                    return SagaRegistrationState.Enabled;
                }

                sawDisabledEnableSagasAssignment = true;
                continue;
            }

            sawUnknownEnableSagasAssignment = true;
        }

        if (sawUnknownEnableSagasAssignment)
        {
            return SagaRegistrationState.Unknown;
        }

        return sawDisabledEnableSagasAssignment
            ? SagaRegistrationState.Disabled
            : SagaRegistrationState.Unknown;
    }

    private static IEnumerable<IOperation> EnumerateOperations(IOperation root)
    {
        var stack = new Stack<IOperation>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            foreach (var child in current.ChildOperations)
            {
                stack.Push(child);
            }
        }
    }

    private static bool IsMiniBusProcessorOptionsProperty(
        IPropertyReferenceOperation propertyReference,
        MiniBusSymbols symbols)
    {
        return IsMiniBusProcessorOptionsProperty(propertyReference.Property, symbols);
    }

    private static bool IsMiniBusProcessorOptionsProperty(
        IPropertySymbol property,
        MiniBusSymbols symbols)
    {
        if (SymbolEqualityComparer.Default.Equals(property.ContainingType, symbols.MiniBusProcessorOptions))
        {
            return true;
        }

        return string.Equals(
            property.ContainingType?.ToDisplayString(),
            MiniBusSymbolNames.MiniBusProcessorOptions,
            StringComparison.Ordinal);
    }

    private static void EnqueueMiniBusProcessorMemberUsageLocations(
        INamedTypeSymbol type,
        INamedTypeSymbol? miniBusProcessor,
        AnalyzerState state)
    {
        if (miniBusProcessor is null)
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IMethodSymbol method:
                    if (SymbolEqualityComparer.Default.Equals(method.ReturnType, miniBusProcessor)
                        || method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, miniBusProcessor)))
                    {
                        EnqueueFirstLocation(method, state.MiniBusProcessorUsageLocations);
                    }

                    break;

                case IPropertySymbol property
                    when SymbolEqualityComparer.Default.Equals(property.Type, miniBusProcessor):
                    EnqueueFirstLocation(property, state.MiniBusProcessorUsageLocations);
                    break;

                case IFieldSymbol field
                    when SymbolEqualityComparer.Default.Equals(field.Type, miniBusProcessor):
                    EnqueueFirstLocation(field, state.MiniBusProcessorUsageLocations);
                    break;
            }
        }
    }

    private static void EnqueueFirstLocation(ISymbol symbol, ConcurrentQueue<Location> locations)
    {
        if (symbol.Locations.FirstOrDefault() is { } location)
        {
            locations.Enqueue(location);
        }
    }

    private static IEnumerable<Location> GetDistinctLocations(IEnumerable<Location> locations)
    {
        var seenLocations = new HashSet<LocationKey>();
        foreach (var location in locations)
        {
            var key = CreateLocationKey(location);
            if (seenLocations.Add(key))
            {
                yield return location;
            }
        }
    }

    private static LocationKey CreateLocationKey(Location location)
    {
        var treeId = location.SourceTree is null
            ? 0
            : RuntimeHelpers.GetHashCode(location.SourceTree);

        return new LocationKey(treeId, location.SourceSpan.Start, location.SourceSpan.Length);
    }

    private readonly struct LocationKey : IEquatable<LocationKey>
    {
        public LocationKey(int treeId, int start, int length)
        {
            TreeId = treeId;
            Start = start;
            Length = length;
        }

        private int TreeId { get; }

        private int Start { get; }

        private int Length { get; }

        public bool Equals(LocationKey other)
        {
            return TreeId == other.TreeId && Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is LocationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TreeId;
                hash = (hash * 397) ^ Start;
                hash = (hash * 397) ^ Length;
                return hash;
            }
        }
    }

    private sealed class MiniBusSymbols
    {
        private MiniBusSymbols(
            INamedTypeSymbol? message,
            INamedTypeSymbol? command,
            INamedTypeSymbol? @event,
            INamedTypeSymbol? handleMessages,
            INamedTypeSymbol? handleSagaMessages,
            INamedTypeSymbol? miniBusSaga,
            INamedTypeSymbol? sagaTimeout,
            INamedTypeSymbol? miniBusContext,
            INamedTypeSymbol? miniBusProcessor,
            INamedTypeSymbol? miniBusProcessorOptions,
            INamedTypeSymbol? azureServiceBusTransportRoutes,
            INamedTypeSymbol? miniBusAzureFunctionsServiceCollectionExtensions)
        {
            IMessage = message;
            ICommand = command;
            IEvent = @event;
            IHandleMessages = handleMessages;
            IHandleSagaMessages = handleSagaMessages;
            MiniBusSaga = miniBusSaga;
            ISagaTimeout = sagaTimeout;
            MiniBusContext = miniBusContext;
            MiniBusProcessor = miniBusProcessor;
            MiniBusProcessorOptions = miniBusProcessorOptions;
            AzureServiceBusTransportRoutes = azureServiceBusTransportRoutes;
            MiniBusAzureFunctionsServiceCollectionExtensions = miniBusAzureFunctionsServiceCollectionExtensions;
        }

        public INamedTypeSymbol? IMessage { get; }

        public INamedTypeSymbol? ICommand { get; }

        public INamedTypeSymbol? IEvent { get; }

        public INamedTypeSymbol? IHandleMessages { get; }

        public INamedTypeSymbol? IHandleSagaMessages { get; }

        public INamedTypeSymbol? MiniBusSaga { get; }

        public INamedTypeSymbol? ISagaTimeout { get; }

        public INamedTypeSymbol? MiniBusContext { get; }

        public INamedTypeSymbol? MiniBusProcessor { get; }

        public INamedTypeSymbol? MiniBusProcessorOptions { get; }

        public INamedTypeSymbol? AzureServiceBusTransportRoutes { get; }

        public INamedTypeSymbol? MiniBusAzureFunctionsServiceCollectionExtensions { get; }

        public static MiniBusSymbols Create(Compilation compilation)
        {
            return new MiniBusSymbols(
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.IMessage),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.ICommand),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.IEvent),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.IHandleMessages),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.IHandleSagaMessages),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.MiniBusSaga),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.ISagaTimeout),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.MiniBusContext),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.MiniBusProcessor),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.MiniBusProcessorOptions),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.AzureServiceBusTransportRoutes),
                compilation.GetTypeByMetadataName(MiniBusSymbolNames.MiniBusAzureFunctionsServiceCollectionExtensions));
        }
    }

    private sealed class AnalyzerState
    {
        private readonly ConcurrentDictionary<RouteKind, ConcurrentDictionary<INamedTypeSymbol, byte>> _routes = new();

        public ConcurrentQueue<MessageUsage> MessageUsages { get; } = new();

        public ConcurrentQueue<Location> MiniBusProcessorUsageLocations { get; } = new();

        public ConcurrentQueue<Location> SagaUsageLocations { get; } = new();

        public ConcurrentQueue<Location> SagaDisabledRegistrationLocations { get; } = new();

        public bool AzureFunctionsRegistrationVisible { get; set; }

        public bool SagasEnabledVisible { get; set; }

        public bool AnyRoutesVisible => _routes.Count > 0;

        public void AddRoute(RouteKind kind, ITypeSymbol messageType)
        {
            if (messageType is not INamedTypeSymbol namedType)
            {
                return;
            }

            var routes = _routes.GetOrAdd(kind, _ => new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default));
            routes.TryAdd(namedType, 0);
        }

        public void AddUsage(RouteKind kind, ITypeSymbol messageType, Location location)
        {
            if (messageType is INamedTypeSymbol namedType)
            {
                MessageUsages.Enqueue(new MessageUsage(kind, namedType, location));
            }
        }

        public bool HasRouteFor(RouteKind kind, INamedTypeSymbol messageType)
        {
            if (ContainsRoute(kind, messageType))
            {
                return true;
            }

            if (kind == RouteKind.Scheduled)
            {
                return ContainsRoute(RouteKind.Command, messageType) || ContainsRoute(RouteKind.Event, messageType);
            }

            return false;
        }

        private bool ContainsRoute(RouteKind kind, INamedTypeSymbol messageType)
        {
            return _routes.TryGetValue(kind, out var routes) && routes.ContainsKey(messageType);
        }
    }

    private sealed class MessageUsage
    {
        public MessageUsage(RouteKind kind, INamedTypeSymbol messageType, Location location)
        {
            Kind = kind;
            MessageType = messageType;
            Location = location;
        }

        public RouteKind Kind { get; }

        public INamedTypeSymbol MessageType { get; }

        public Location Location { get; }
    }
}

internal enum RouteKind
{
    None,
    Command,
    Event,
    Scheduled
}

internal enum SagaRegistrationState
{
    Disabled,
    Enabled,
    Unknown
}

internal static class RouteKindExtensions
{
    public static string ToDiagnosticText(this RouteKind kind)
    {
        return kind switch
        {
            RouteKind.Command => "send",
            RouteKind.Event => "publish",
            RouteKind.Scheduled => "schedule",
            _ => "message"
        };
    }
}
