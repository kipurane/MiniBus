using MiniBus.Core.Contracts;

namespace MiniBus.Core.Identity;

public sealed class MiniBusMessageIdentityRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Type, string> _primaryByType = new();
    private readonly Dictionary<string, Type> _typeByIdentity = new(StringComparer.Ordinal);

    public MiniBusMessageIdentityRegistry Map<TMessage>(string identity, params string[] aliases)
        where TMessage : IMessage
        => Map(typeof(TMessage), identity, aliases);

    public MiniBusMessageIdentityRegistry Map(Type messageType, string identity, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(aliases);
        EnsureMessageType(messageType, nameof(messageType));
        ValidateIdentity(identity, nameof(identity));

        foreach (var alias in aliases)
        {
            ValidateIdentity(alias, nameof(aliases));
        }

        lock (_sync)
        {
            var identities = new HashSet<string>(StringComparer.Ordinal) { identity };
            foreach (var alias in aliases)
            {
                if (!identities.Add(alias))
                {
                    throw new InvalidOperationException($"MiniBus message identity '{alias}' is repeated.");
                }
            }

            if (_primaryByType.TryGetValue(messageType, out var existingPrimary)
                && !string.Equals(existingPrimary, identity, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"MiniBus message type '{messageType.FullName}' is already mapped to primary identity '{existingPrimary}' and cannot also map to '{identity}'.");
            }

            EnsureAvailable(identity, messageType);
            foreach (var alias in aliases)
            {
                EnsureAvailable(alias, messageType);
            }

            _primaryByType[messageType] = identity;
            _typeByIdentity[identity] = messageType;
            foreach (var alias in aliases)
            {
                _typeByIdentity[alias] = messageType;
            }
        }

        return this;
    }

    public string GetIdentity(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        EnsureMessageType(messageType, nameof(messageType));

        lock (_sync)
        {
            if (_primaryByType.TryGetValue(messageType, out var identity))
            {
                return identity;
            }
        }

        return messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name;
    }

    public Type Resolve(string identity)
    {
        ValidateIdentity(identity, nameof(identity));

        lock (_sync)
        {
            if (_typeByIdentity.TryGetValue(identity, out var registeredType))
            {
                return registeredType;
            }
        }

        var resolvedType = Type.GetType(identity, throwOnError: false);
        if (resolvedType is null || !typeof(IMessage).IsAssignableFrom(resolvedType))
        {
            throw new InvalidOperationException($"MiniBus message identity '{identity}' could not be resolved to an IMessage contract type.");
        }

        return resolvedType;
    }

    private void EnsureAvailable(string identity, Type messageType)
    {
        if (_typeByIdentity.TryGetValue(identity, out var existingType))
        {
            throw new InvalidOperationException(
                $"MiniBus message identity '{identity}' is already mapped to '{existingType.FullName}' and cannot also map to '{messageType.FullName}'.");
        }
    }

    private static void ValidateIdentity(string identity, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            throw new ArgumentException("MiniBus message identity cannot be empty or whitespace.", parameterName);
        }
    }

    private static void EnsureMessageType(Type messageType, string parameterName)
    {
        if (!typeof(IMessage).IsAssignableFrom(messageType))
        {
            throw new ArgumentException($"Type '{messageType.FullName}' must implement IMessage.", parameterName);
        }
    }
}
