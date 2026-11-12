using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.AzureServiceBus.Tests;

public sealed class StableMessageIdentityTransportTests
{
    [Fact]
    public void MessageFactory_UsesPrimaryStableIdentityAndNeverAlias()
    {
        var identities = new MiniBusMessageIdentityRegistry()
            .Map<TestCommand>("orders.submit", "Legacy.SubmitOrder, Legacy.Contracts");
        var factory = new AzureServiceBusMessageFactory(new Serializer(), messageIdentities: identities);

        var message = factory.CreateMessage(new TestCommand(), typeof(TestCommand), new Dictionary<string, string>
        {
            [MiniBusHeaderNames.MessageType] = "incorrect",
            [MiniBusHeaderNames.EnclosedMessageTypes] = "incorrect"
        });

        Assert.Equal("orders.submit", message.Subject);
        Assert.Equal("orders.submit", message.ApplicationProperties[MiniBusHeaderNames.MessageType]);
        Assert.Equal("orders.submit", message.ApplicationProperties[MiniBusHeaderNames.EnclosedMessageTypes]);
    }

    [Fact]
    public void MessageFactory_UnregisteredContractRetainsClrIdentity()
    {
        var factory = new AzureServiceBusMessageFactory(new Serializer());

        var message = factory.CreateMessage(new TestCommand(), typeof(TestCommand));

        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, message.Subject);
    }

    [Fact]
    public void StoredBodyReplay_PreservesHistoricalIdentity()
    {
        var identities = new MiniBusMessageIdentityRegistry()
            .Map<TestCommand>("orders.submit", "legacy.order");
        var factory = new AzureServiceBusMessageFactory(new Serializer(), messageIdentities: identities);
        var message = factory.CreateMessage(BinaryData.FromString("{}"), typeof(TestCommand),
            new Dictionary<string, string>
            {
                [MiniBusHeaderNames.MessageType] = "legacy.order",
                [MiniBusHeaderNames.EnclosedMessageTypes] = "legacy.order"
            });

        Assert.Equal("legacy.order", message.Subject);
        Assert.Equal("legacy.order", message.ApplicationProperties[MiniBusHeaderNames.MessageType]);
        Assert.Equal("legacy.order", message.ApplicationProperties[MiniBusHeaderNames.EnclosedMessageTypes]);
    }

    private sealed record TestCommand : ICommand;

    private sealed class Serializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType) => BinaryData.FromString("{}");
        public object Deserialize(BinaryData body, Type messageType) => throw new NotSupportedException();
    }
}
