# MiniBus.AzureFunctions.SourceGenerators

`MiniBus.AzureFunctions.SourceGenerators` generates thin Azure Functions isolated worker Service Bus trigger wrappers for MiniBus applications.

Manual wrappers remain fully supported. Use this package when an endpoint has ordinary Service Bus queue or topic/subscription triggers and you want MiniBus to generate the repetitive wrapper class that delegates to `MiniBusProcessor`.

## Package Reference

Reference the package as an analyzer/source-generator package from the Function App project:

```xml
<PackageReference Include="MiniBus.AzureFunctions.SourceGenerators" Version="0.1.0-preview.1" PrivateAssets="all" OutputItemType="Analyzer" />
```

The application still references `MiniBus.AzureFunctions` and the normal Azure Functions worker packages at runtime.

The source generator injects declaration attributes into the reserved `MiniBus.AzureFunctions.SourceGenerators.Declarations` namespace. Applications should use those generated attributes but should not define their own types in that namespace.

## Queue Wrapper

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusQueueFunction(
    functionName: "BillingInput",
    queueName: "billing-queue",
    connection: "ServiceBus")]
```

This generates a Functions wrapper equivalent to a manual class with `[Function("BillingInput")]`, `[ServiceBusTrigger("billing-queue", Connection = "ServiceBus")]`, and a call to `MiniBusProcessor.ProcessAsync`.

Constructor-style arguments such as `functionName:` and property-assignment arguments such as `FunctionName =` are both supported.

Generated wrapper types are emitted into the reserved `MiniBus.AzureFunctions.__Generated` namespace. Applications should not define their own types in either reserved source-generator namespace.

## Topic Subscription Wrapper

```csharp
using MiniBus.AzureFunctions.SourceGenerators.Declarations;

[assembly: MiniBusSourceGeneratedServiceBusTopicFunction(
    functionName: "BillingEvents",
    topicName: "domain-events",
    subscriptionName: "billing",
    connection: "ServiceBus")]
```

## Diagnostics

The generator reports compile-time diagnostics for empty required values, required values that are not compile-time constant strings, and duplicate function names. Invalid declarations do not emit wrapper source.

## Limits

The generator does not create queues, topics, subscriptions, filters, handlers, message contracts, routes, or saga-specific code. It only generates the same thin wrapper shape that an application can write manually.
