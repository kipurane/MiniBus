namespace MiniBus.Core.Handlers;

internal sealed record HandlerRegistration(Type MessageType, Type ServiceType, Type HandlerType);

