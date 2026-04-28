using MiniBus.Core.Contracts;

namespace MiniBus.Core.Routing;

internal sealed class CommandRouteRegistry
{
    private readonly Dictionary<Type, string> _routes = new();

    public void Map<TCommand>(string destination)
        where TCommand : ICommand
    {
        Map(typeof(TCommand), destination);
    }

    public void Map(Type commandType, string destination)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("Destination must be provided.", nameof(destination));
        }

        if (!typeof(ICommand).IsAssignableFrom(commandType))
        {
            throw new ArgumentException($"Type '{commandType.FullName}' must implement {typeof(ICommand).FullName}.", nameof(commandType));
        }

        if (_routes.TryGetValue(commandType, out var existingDestination))
        {
            if (!string.Equals(existingDestination, destination, StringComparison.Ordinal))
            {
                throw new CommandRouteConflictException(commandType, existingDestination, destination);
            }

            return;
        }

        _routes.Add(commandType, destination);
    }

    public string GetDestination<TCommand>()
        where TCommand : ICommand
    {
        return GetDestination(typeof(TCommand));
    }

    public string GetDestination(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        if (_routes.TryGetValue(commandType, out var destination))
        {
            return destination;
        }

        throw new CommandRouteNotFoundException(commandType);
    }
}

