using Microsoft.Extensions.DependencyInjection;

namespace MiniBus.Samples.FunctionApp;

public static class Program
{
    public static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        return services.AddBillingMiniBus();
    }
}
