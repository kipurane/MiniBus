using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var acceptEula = GetEulaAcceptance(builder.Configuration);
var mssqlSaPassword = builder.AddParameter("mssql-sa-password", secret: true);
var serviceBusConnection = builder.AddParameter("servicebus-connection-string", secret: true);
var billingSql = builder.AddParameter("billing-sql", secret: true);
var billingSqlSchema = builder.AddParameter("billing-sql-schema");
var functionsStorage = builder.AddParameter("functions-storage-connection-string", secret: true);
var outboxDispatchSchedule = builder.AddParameter("billing-outbox-dispatch-schedule");
var outboxDispatchMaxBatches = builder.AddParameter("billing-outbox-dispatch-max-batches");

var sql = builder.AddContainer(AppHostResourceNames.Sql, "mcr.microsoft.com/mssql/server", "2022-latest")
    .WithContainerName("minibus-aspire-billing-servicebus-emulator-sql")
    .WithPersistentLifetime()
    .WithContainerRuntimeArgs("--platform", "linux/amd64")
    .WithEnvironment("ACCEPT_EULA", acceptEula)
    .WithEnvironment("MSSQL_SA_PASSWORD", mssqlSaPassword)
    .WithEndpoint(targetPort: 1433, port: 14333, scheme: "tcp", name: "sql", isExternal: true, isProxied: false);

var functionsStorageContainer = builder.AddContainer(AppHostResourceNames.FunctionsStorage, "mcr.microsoft.com/azure-storage/azurite", "3.35.0")
    .WithContainerName("minibus-aspire-billing-functions-storage")
    .WithPersistentLifetime()
    .WithEndpoint(targetPort: 10000, port: 10000, scheme: "http", name: "blob", isExternal: true, isProxied: false)
    .WithEndpoint(targetPort: 10001, port: 10001, scheme: "http", name: "queue", isExternal: true, isProxied: false)
    .WithEndpoint(targetPort: 10002, port: 10002, scheme: "http", name: "table", isExternal: true, isProxied: false);

var serviceBusEmulator = builder.AddContainer(AppHostResourceNames.ServiceBusEmulator, "mcr.microsoft.com/azure-messaging/servicebus-emulator", "latest")
    .WithContainerName("minibus-aspire-billing-servicebus-emulator")
    .WithPersistentLifetime()
    .WithBindMount("../MiniBus.Samples.Billing.FunctionApp/servicebus-emulator/Config.json", "/ServiceBus_Emulator/ConfigFiles/Config.json", isReadOnly: true)
    .WithEnvironment("ACCEPT_EULA", acceptEula)
    .WithEnvironment("EMULATOR_HTTP_PORT", "5300")
    .WithEnvironment("MSSQL_SA_PASSWORD", mssqlSaPassword)
    .WithEnvironment("SQL_SERVER", AppHostResourceNames.Sql)
    .WithEndpoint(targetPort: 5672, port: 5672, scheme: "tcp", name: "amqp", isExternal: true, isProxied: false)
    .WithEndpoint(targetPort: 5300, port: 5300, scheme: "http", name: "http", isExternal: true, isProxied: false)
    .WaitForStart(sql);

var billingFunctionAppDirectory = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "../MiniBus.Samples.Billing.FunctionApp"));
var billingFunctionAppDll = Path.Combine(
    billingFunctionAppDirectory,
    "bin/Debug/net10.0/MiniBus.Samples.Billing.FunctionApp.dll");

var billingSqlSchemaApplier = builder
    .AddExecutable(
        AppHostResourceNames.BillingSqlSchema,
        "dotnet",
        billingFunctionAppDirectory,
        billingFunctionAppDll,
        "--apply-sql-schema")
    .WithEnvironment("BillingSql", billingSql)
    .WithEnvironment("BillingSqlSchema", billingSqlSchema)
    .WaitForStart(sql);

builder.AddProject<Projects.MiniBus_Samples_Billing_FunctionApp>(AppHostResourceNames.BillingFunctionApp)
    .WithArgs("--port", "7071")
    .WithEnvironment("AzureWebJobsStorage", functionsStorage)
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("ServiceBus", serviceBusConnection)
    .WithEnvironment("BillingSqlEnabled", "true")
    .WithEnvironment("BillingSql", billingSql)
    .WithEnvironment("BillingSqlSchema", billingSqlSchema)
    .WaitForCompletion(billingSqlSchemaApplier)
    .WaitForStart(serviceBusEmulator)
    .WaitForStart(functionsStorageContainer);

builder.AddProject<Projects.MiniBus_Samples_Inventory_FunctionApp>(AppHostResourceNames.InventoryFunctionApp)
    .WithArgs("--port", "7072")
    .WithEnvironment("AzureWebJobsStorage", functionsStorage)
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("ServiceBus", serviceBusConnection)
    .WaitForStart(serviceBusEmulator)
    .WaitForStart(functionsStorageContainer);

builder.AddProject<Projects.MiniBus_Samples_Billing_OutboxDispatcher_FunctionApp>(AppHostResourceNames.BillingOutboxDispatcher)
    .WithArgs("--port", "7073")
    .WithEnvironment("AzureWebJobsStorage", functionsStorage)
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("ServiceBus", serviceBusConnection)
    .WithEnvironment("BillingSqlEnabled", "true")
    .WithEnvironment("BillingSql", billingSql)
    .WithEnvironment("BillingSqlSchema", billingSqlSchema)
    .WithEnvironment("BillingOutboxDispatchSchedule", outboxDispatchSchedule)
    .WithEnvironment("BillingOutboxDispatchMaxBatches", outboxDispatchMaxBatches)
    .WaitForCompletion(billingSqlSchemaApplier)
    .WaitForStart(serviceBusEmulator)
    .WaitForStart(functionsStorageContainer);

builder.AddProject<Projects.MiniBus_Tooling_Web>(AppHostResourceNames.ToolingWeb)
    .WithEnvironment("MiniBus__Tooling__Sql__ConnectionString", billingSql)
    .WithEnvironment("MiniBus__Tooling__Sql__SchemaName", billingSqlSchema)
    .WaitForCompletion(billingSqlSchemaApplier)
    .WaitForStart(sql);

builder.Build().Run();

static string GetEulaAcceptance(IConfiguration configuration)
{
    var value = configuration["ACCEPT_EULA"]
        ?? configuration["Parameters:accept-eula"]
        ?? "N";

    if (!string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "MiniBus local orchestration starts SQL Server and the Azure Service Bus emulator. " +
            "Accept their local development EULAs by starting the AppHost with ACCEPT_EULA=Y " +
            "or --Parameters:accept-eula=Y.");
    }

    return "Y";
}

internal static class AppHostResourceNames
{
    public const string BillingFunctionApp = "billing-functionapp";
    public const string InventoryFunctionApp = "inventory-functionapp";
    public const string BillingOutboxDispatcher = "billing-outbox-dispatcher";
    public const string ToolingWeb = "minibus-tooling-web";

    public const string BillingSqlSchema = "billing-sql-schema-apply";
    public const string ServiceBusEmulator = "servicebus-emulator";
    public const string Sql = "sql";
    public const string FunctionsStorage = "functions-storage";
}
