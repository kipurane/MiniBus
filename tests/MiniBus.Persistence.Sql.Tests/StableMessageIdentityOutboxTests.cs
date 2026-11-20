using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Identity;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class StableMessageIdentityOutboxTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Serialize_ColumnAndHeadersUseTheIdentitySelectedBeforeSerialization(bool useAsync)
    {
        var identities = new MiniBusMessageIdentityRegistry();
        var serializer = new SqlOutboxOperationSerializer(new Serializer
        {
            OnSerialize = () => identities.Map<TestCommand>("new.identity")
        }, messageIdentities: identities);

        var serialized = useAsync
            ? await serializer.SerializeAsync(CreateOperation())
            : serializer.Serialize(CreateOperation());

        var headers = SqlOutboxOperationSerializer.DeserializeHeaders(serialized.HeadersJson);
        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, serialized.MessageType);
        Assert.Equal(serialized.MessageType, headers[MiniBusHeaderNames.MessageType]);
        Assert.Equal(serialized.MessageType, headers[MiniBusHeaderNames.EnclosedMessageTypes]);
        Assert.Equal("new.identity", identities.GetIdentity(typeof(TestCommand)));
    }

    [Fact]
    public void SerializeAndDeserialize_UsesStableIdentity()
    {
        var identities = new MiniBusMessageIdentityRegistry().Map<TestCommand>("orders.submit");
        var serializer = new SqlOutboxOperationSerializer(new Serializer(), messageIdentities: identities);

        var serialized = serializer.Serialize(CreateOperation());
        var stored = Deserialize(serializer, serialized);

        Assert.Equal("orders.submit", serialized.MessageType);
        Assert.Equal(typeof(TestCommand), stored.MessageType);
        Assert.Equal("orders.submit", stored.Headers[MiniBusHeaderNames.MessageType]);
    }

    [Fact]
    public void Deserialize_AliasResolvesRenamedContractAndPreservesStoredHeader()
    {
        const string historicalIdentity = "Legacy.SubmitOrder, Legacy.Contracts";
        var identities = new MiniBusMessageIdentityRegistry()
            .Map<TestCommand>("orders.submit", historicalIdentity);
        var serializer = new SqlOutboxOperationSerializer(new Serializer(), messageIdentities: identities);
        var headers = SqlOutboxOperationSerializer.SerializeHeaders(new Dictionary<string, string>
        {
            [MiniBusHeaderNames.MessageType] = historicalIdentity,
            [MiniBusHeaderNames.EnclosedMessageTypes] = historicalIdentity
        });

        var stored = serializer.Deserialize(Guid.NewGuid(), "outgoing-1", "Send", historicalIdentity, [], headers, null, 0);

        Assert.Equal(typeof(TestCommand), stored.MessageType);
        Assert.Equal(historicalIdentity, stored.Headers[MiniBusHeaderNames.MessageType]);
    }

    [Fact]
    public void Deserialize_LegacyClrIdentityRemainsSupported()
    {
        var serializer = new SqlOutboxOperationSerializer(new Serializer());
        var serialized = serializer.Serialize(CreateOperation());

        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, serialized.MessageType);
        Assert.Equal(typeof(TestCommand), Deserialize(serializer, serialized).MessageType);
    }

    [Fact]
    public void Deserialize_UnknownIdentityFailsClearly()
    {
        var serializer = new SqlOutboxOperationSerializer(new Serializer());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serializer.Deserialize(Guid.NewGuid(), "outgoing-1", "Send", "unknown", [], "{}", null, 0));

        Assert.Contains("unknown", exception.Message, StringComparison.Ordinal);
    }

    private static MiniBusOutboxOperation CreateOperation() => new(
        MiniBusOutboxOperationKind.Send,
        new TestCommand(),
        typeof(TestCommand),
        new Dictionary<string, string>(),
        null);

    private static MiniBusOutboxStoredOperation Deserialize(SqlOutboxOperationSerializer serializer, SerializedOutboxOperation operation) =>
        serializer.Deserialize(Guid.NewGuid(), "outgoing-1", operation.OperationKind, operation.MessageType,
            operation.Body, operation.HeadersJson, operation.DueTime, 0);

    private sealed record TestCommand : ICommand;

    private sealed class Serializer : IMessageSerializer
    {
        public Action? OnSerialize { get; init; }

        public BinaryData Serialize(object message, Type messageType)
        {
            OnSerialize?.Invoke();
            return BinaryData.FromString("{}");
        }
        public object Deserialize(BinaryData body, Type messageType) => throw new NotSupportedException();
    }
}
