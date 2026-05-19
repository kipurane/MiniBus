# MiniBus.Analyzers

Roslyn analyzers for common MiniBus configuration, routing, handler, and message contract mistakes.

```xml
<PackageReference Include="MiniBus.Analyzers" Version="0.1.0-preview.1" PrivateAssets="all" OutputItemType="Analyzer" />
```

The analyzer package is optional developer tooling. It runs during normal C# builds and does not add runtime dependencies to MiniBus applications.

## Diagnostics

| ID | Severity | Description |
| --- | --- | --- |
| MBAN001 | Warning | MiniBus handler type is abstract and cannot be instantiated. |
| MBAN002 | Warning | MiniBus handler type is an open generic in a visible registration or discovery context. |
| MBAN003 | Warning | MiniBus message type implements both `ICommand` and `IEvent`. |
| MBAN004 | Warning | Type is used with a MiniBus API that expects a different message contract. |
| MBAN005 | Warning | Azure Service Bus route destination is empty or whitespace. |
| MBAN006 | Warning | A visible send, publish, or schedule call has no matching visible route. |
| MBAN007 | Warning | MiniBus Azure Functions processor usage is visible without visible `AddMiniBusAzureFunctions` registration. |
| MBAN008 | Warning | Saga usage is visible but saga processing is visibly disabled. |

## Examples

```csharp
public sealed record Ambiguous(Guid Id) : ICommand, IEvent;
```

`MBAN003` reports that the message role is ambiguous. Use one marker contract for each message.

```csharp
routes.MapCommand<CreateInvoice>(" ");
```

`MBAN005` reports that the route destination is empty.

## Suppression

Suppress diagnostics intentionally through normal Roslyn mechanisms such as `.editorconfig`, `#pragma warning disable`, or `NoWarn` when an application uses dynamic configuration that the analyzer cannot see.

The first analyzer release is deliberately conservative. It avoids whole-program dependency injection validation and skips route, Azure Functions, and saga diagnostics when the relevant configuration is not statically visible.
