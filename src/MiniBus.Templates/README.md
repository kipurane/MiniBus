# MiniBus.Templates

`MiniBus.Templates` contains `dotnet new` starters for MiniBus applications.

The first template creates an Azure Functions isolated-worker project with Azure Service Bus transport:

```bash
dotnet new install MiniBus.Templates
dotnet new minibus-functionapp -n Contoso.Orders.FunctionApp
```

The generated project includes a manual Service Bus trigger wrapper, MiniBus registration, starter contracts, one handler, Azure Service Bus routes, recoverability defaults, and a local README for the application-owned configuration steps.

MiniBus package publishing is still a repository workflow step. From this repository, pack the template and install the local `.nupkg` when validating the package shape:

```bash
dotnet pack src/MiniBus.Templates/MiniBus.Templates.csproj -c Release
dotnet new install artifacts/packages/MiniBus.Templates.0.1.0-preview.1.nupkg
```

The v1 template is intentionally focused. SQL persistence, sagas, generated trigger wrappers, Azure resource provisioning, and deployment automation remain follow-up choices after the starter project exists.
