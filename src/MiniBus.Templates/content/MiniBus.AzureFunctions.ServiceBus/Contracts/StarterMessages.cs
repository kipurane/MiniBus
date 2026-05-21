using MiniBus.Core.Contracts;

namespace MiniBus.FunctionApp.Template.Contracts;

public sealed record SubmitOrder(string OrderId, string CustomerId) : ICommand;

public sealed record OrderSubmitted(string OrderId, string CustomerId) : IEvent;
