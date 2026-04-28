namespace MiniBus.Core.Routing;

internal sealed class CommandRouteNotFoundException : InvalidOperationException
{
    public CommandRouteNotFoundException(Type commandType)
        : base($"No route has been configured for command '{commandType.FullName}'.")
    {
        CommandType = commandType;
    }

    public Type CommandType { get; }
}

