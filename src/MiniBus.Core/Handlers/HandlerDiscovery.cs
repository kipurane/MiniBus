using System.Reflection;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Handlers;

internal static class HandlerDiscovery
{
    public static IReadOnlyList<HandlerRegistration> Discover(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return Discover((IEnumerable<Assembly>)assemblies);
    }

    public static IReadOnlyList<HandlerRegistration> Discover(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registrations = new List<HandlerRegistration>();
        var seenRegistrations = new HashSet<(Type ServiceType, Type HandlerType)>();

        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.DefinedTypes)
            {
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                foreach (var handlerInterface in type.ImplementedInterfaces.Where(IsHandlerInterface))
                {
                    var messageType = handlerInterface.GetGenericArguments()[0];
                    if (!typeof(IMessage).IsAssignableFrom(messageType))
                    {
                        continue;
                    }

                    var serviceType = typeof(IHandleMessages<>).MakeGenericType(messageType);
                    var handlerType = type.AsType();
                    if (seenRegistrations.Add((serviceType, handlerType)))
                    {
                        registrations.Add(new HandlerRegistration(messageType, serviceType, handlerType));
                    }
                }
            }
        }

        return registrations;
    }

    private static bool IsHandlerInterface(Type implementedInterface)
    {
        return implementedInterface.IsGenericType
               && implementedInterface.GetGenericTypeDefinition() == typeof(IHandleMessages<>);
    }
}

