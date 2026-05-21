using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MiniBus.Samples.FunctionApp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (BillingSampleSeeder.IsSeedCommand(args))
        {
            var seed = await BillingSampleSeeder
                .SendCreateInvoiceAsync(BillingSampleServiceBusConnection.GetSeedConnectionString())
                .ConfigureAwait(false);

            Console.WriteLine(
                $"Seeded CreateInvoice '{seed.InvoiceId}' for customer '{seed.CustomerId}' " +
                $"with Service Bus message '{seed.MessageId}'.");
            return;
        }

        var builder = FunctionsApplication.CreateBuilder(args);

        ConfigureServices(builder.Services, builder.Configuration);

        builder.Build().Run();
    }

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddBillingMiniBus(configuration);
    }
}
