
namespace MiniBus.Core.Routing;

internal sealed class CommandRouteConflictException : InvalidOperationException
{
    public CommandRouteConflictException(Type commandType, string existingDestination, string attemptedDestination)
        : base($"Command '{commandType.FullName}' is already mapped to '{existingDestination}' and cannot also be mapped to '{attemptedDestination}'.")
    {
        CommandType = commandType;
        ExistingDestination = existingDestination;
        AttemptedDestination = attemptedDestination;
    }

    public Type CommandType { get; }

    public string ExistingDestination { get; }

    public string AttemptedDestination { get; }
}

