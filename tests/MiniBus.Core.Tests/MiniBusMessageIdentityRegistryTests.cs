using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class MiniBusMessageIdentityRegistryTests
{
    [Fact]
    public void Map_PrimaryAndAliasesResolveWhileOnlyPrimaryIsOutgoing()
    {
        var registry = new MiniBusMessageIdentityRegistry()
            .Map<TestCommand>("orders.submit", "Legacy.SubmitOrder, Legacy.Contracts");

        Assert.Equal("orders.submit", registry.GetIdentity(typeof(TestCommand)));
        Assert.Equal(typeof(TestCommand), registry.Resolve("orders.submit"));
        Assert.Equal(typeof(TestCommand), registry.Resolve("Legacy.SubmitOrder, Legacy.Contracts"));
    }

    [Fact]
    public void UnregisteredContract_UsesAssemblyQualifiedFallbackBothWays()
    {
        var registry = new MiniBusMessageIdentityRegistry();
        var identity = typeof(TestCommand).AssemblyQualifiedName!;

        Assert.Equal(identity, registry.GetIdentity(typeof(TestCommand)));
        Assert.Equal(typeof(TestCommand), registry.Resolve(identity));
    }

    [Fact]
    public void Map_IsCaseSensitive()
    {
        var registry = new MiniBusMessageIdentityRegistry()
            .Map<TestCommand>("orders.submit")
            .Map<TestEvent>("Orders.Submit");

        Assert.Equal(typeof(TestCommand), registry.Resolve("orders.submit"));
        Assert.Equal(typeof(TestEvent), registry.Resolve("Orders.Submit"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_RejectsBlankIdentity(string identity)
    {
        Assert.Throws<ArgumentException>(() => new MiniBusMessageIdentityRegistry().Map<TestCommand>(identity));
    }

    [Fact]
    public void Map_RejectsNonMessageType()
    {
        Assert.Throws<ArgumentException>(() => new MiniBusMessageIdentityRegistry().Map(typeof(string), "text"));
    }

    [Fact]
    public void Map_RejectsIdentityConflictAcrossTypes()
    {
        var registry = new MiniBusMessageIdentityRegistry().Map<TestCommand>("shared");

        Assert.Throws<InvalidOperationException>(() => registry.Map<TestEvent>("shared"));
    }

    [Fact]
    public void Map_RejectsRepeatedIdentitiesWithoutChangingRegistry()
    {
        var registry = new MiniBusMessageIdentityRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Map<TestCommand>("primary", "alias", "alias"));
        Assert.Throws<InvalidOperationException>(() => registry.Map<TestCommand>("primary", "primary"));
        registry.Map<TestCommand>("primary", "alias");
        Assert.Throws<InvalidOperationException>(() => registry.Map<TestCommand>("primary"));
        Assert.Equal(typeof(TestCommand), registry.Resolve("alias"));
    }

    [Fact]
    public void Map_RejectsMultiplePrimaryIdentitiesForType()
    {
        var registry = new MiniBusMessageIdentityRegistry().Map<TestCommand>("orders.submit");

        Assert.Throws<InvalidOperationException>(() => registry.Map<TestCommand>("orders.submit.v2"));
    }

    [Fact]
    public void Resolve_RejectsUnknownAndNonMessageFallbackTypes()
    {
        var registry = new MiniBusMessageIdentityRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Resolve("unknown.contract"));
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(typeof(string).AssemblyQualifiedName!));
    }

    private sealed record TestCommand : ICommand;
    private sealed record TestEvent : IEvent;
}
