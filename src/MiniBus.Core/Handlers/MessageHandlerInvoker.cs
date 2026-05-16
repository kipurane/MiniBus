using System.Collections;
using MiniBus.Core.Context;
using MiniBus.Core.Contracts;

namespace MiniBus.Core.Handlers;

public sealed class MessageHandlerInvoker
{
    public async Task InvokeAsync(
        object message,
        MiniBusContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        Action<Type>? handlerInvoked = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var messageType = message.GetType();
        var handlerContractType = typeof(IHandleMessages<>).MakeGenericType(messageType);
        var enumerableHandlerType = typeof(IEnumerable<>).MakeGenericType(handlerContractType);
        var resolvedHandlers = serviceProvider.GetService(enumerableHandlerType);

        if (resolvedHandlers is not IEnumerable handlers)
        {
            return;
        }

        var handleMethod = handlerContractType.GetMethod(nameof(IHandleMessages<IMessage>.Handle))
                          ?? throw new InvalidOperationException($"Handler contract '{handlerContractType.FullName}' does not define a Handle method.");

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            handlerInvoked?.Invoke(handler.GetType());
            var invocationResult = handleMethod.Invoke(handler, new[] { message, context, cancellationToken });
            if (invocationResult is not Task task)
            {
                throw new InvalidOperationException($"Handler '{handler.GetType().FullName}' did not return a Task.");
            }

            await task.ConfigureAwait(false);
        }
    }
}
