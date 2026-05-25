using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniBus.Samples.Billing.FunctionApp;

namespace MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp;

public static class Program
{
    public static void Main(string[] args)
    {
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
