using System.Runtime.ExceptionServices;

namespace MiniBus.AzureServiceBus.IntegrationTests;

public abstract class LiveServiceBusTestBase : IAsyncLifetime
{
    private readonly List<LiveServiceBusScope> _scopes = [];

    private protected LiveServiceBusScope CreateScope(string scenario)
    {
        var scope = new LiveServiceBusScope(scenario);
        _scopes.Add(scope);
        return scope;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        List<Exception> failures = [];

        foreach (var scope in _scopes.AsEnumerable().Reverse())
        {
            try
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
            return;
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Multiple live Azure Service Bus scopes failed to clean up.", failures);
        }
    }
}
