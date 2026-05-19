using Microsoft.CodeAnalysis;

namespace MiniBus.Analyzers;

internal static class MiniBusDiagnosticDescriptors
{
    private const string Category = "MiniBus";

    public static readonly DiagnosticDescriptor AbstractHandler = new(
        id: "MBAN001",
        title: "MiniBus handler type must be concrete",
        messageFormat: "MiniBus handler type '{0}' is abstract and cannot be instantiated",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OpenGenericHandler = new(
        id: "MBAN002",
        title: "MiniBus handler registration uses an open generic handler",
        messageFormat: "MiniBus handler type '{0}' remains open generic in visible registration or discovery",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousMessageContract = new(
        id: "MBAN003",
        title: "MiniBus message type has an ambiguous role",
        messageFormat: "MiniBus message type '{0}' implements both ICommand and IEvent",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MessageContractMismatch = new(
        id: "MBAN004",
        title: "MiniBus API expects a different message contract",
        messageFormat: "MiniBus {0} API expects '{1}' to implement {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyRouteDestination = new(
        id: "MBAN005",
        title: "MiniBus route destination must not be empty",
        messageFormat: "MiniBus {0} route for '{1}' uses an empty or whitespace destination",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingVisibleRoute = new(
        id: "MBAN006",
        title: "MiniBus message has no visible route",
        messageFormat: "MiniBus {0} call for '{1}' has no matching visible route",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor MissingAzureFunctionsRegistration = new(
        id: "MBAN007",
        title: "MiniBus Azure Functions processing is not visibly registered",
        messageFormat: "MiniBusProcessor is used but AddMiniBusAzureFunctions is not visible in this compilation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor SagaProcessingDisabled = new(
        id: "MBAN008",
        title: "MiniBus saga processing is visibly disabled",
        messageFormat: "MiniBus saga usage is visible but this AddMiniBusAzureFunctions registration does not enable sagas",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });
}
