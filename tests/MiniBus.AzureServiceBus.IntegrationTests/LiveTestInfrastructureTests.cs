namespace MiniBus.AzureServiceBus.IntegrationTests;

public sealed class LiveTestInfrastructureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("0")]
    public void Eligibility_RequiresExplicitTrueOptIn(string? optIn)
    {
        var result = LiveServiceBusTestSettings.Evaluate(optIn, "Endpoint=sb://example/;SharedAccessKey=secret");

        Assert.False(result.IsEnabled);
        Assert.Contains(LiveServiceBusTestSettings.OptInEnvironmentVariable, result.SkipReason);
        Assert.DoesNotContain("secret", result.SkipReason);
    }

    [Fact]
    public void Eligibility_RequiresConnectionStringAfterOptIn()
    {
        var result = LiveServiceBusTestSettings.Evaluate("true", null);

        Assert.False(result.IsEnabled);
        Assert.Contains(LiveServiceBusTestSettings.ConnectionStringEnvironmentVariable, result.SkipReason);
    }

    [Fact]
    public void Eligibility_EnablesOnlyWhenBothGatesArePresent()
    {
        var result = LiveServiceBusTestSettings.Evaluate(
            "true",
            "Endpoint=sb://example/;SharedAccessKeyName=owner;SharedAccessKey=secret");

        Assert.True(result.IsEnabled);
        Assert.Null(result.SkipReason);
    }

    [Fact]
    public void Diagnostics_RedactsCompleteSecretAndEmbeddedSharedAccessKey()
    {
        const string connectionString =
            "Endpoint=sb://example/;SharedAccessKeyName=owner;SharedAccessKey=top-secret";
        var value = $"Setup failed for {connectionString}; secondary SharedAccessKey=another-secret";

        var redacted = LiveServiceBusDiagnostics.Redact(value, connectionString);

        Assert.DoesNotContain(connectionString, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("another-secret", redacted, StringComparison.Ordinal);
        Assert.Contains(LiveServiceBusDiagnostics.Redacted, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanupFailure_IsSurfacedWithSafeDiagnostics()
    {
        var exception = Assert.Throws<LiveServiceBusCleanupException>(() =>
            LiveServiceBusCleanupException.ThrowIfAny(
                ["Queue 'minibus-live-q-safe': SharedAccessKey=[REDACTED]"]));

        Assert.Contains("minibus-live-q-safe", exception.Message, StringComparison.Ordinal);
        Assert.Contains(LiveServiceBusDiagnostics.Redacted, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NameFactory_CreatesValidDistinctNames()
    {
        var factory = new LiveServiceBusNameFactory("Run TOKEN");

        var first = factory.Queue("Send command / metadata");
        var second = factory.Queue("Send command / metadata");
        var topic = factory.Topic("Publish:event");

        Assert.StartsWith($"{LiveServiceBusNameFactory.Prefix}-q-run-token-", first);
        Assert.Matches("^[a-z0-9-]+$", first);
        Assert.NotEqual(first, second);
        Assert.Matches("^[a-z0-9-]+$", topic);
    }

    [Fact]
    public void NameFactory_RespectsEntitySpecificLimitsAndRetainsHash()
    {
        var factory = new LiveServiceBusNameFactory("run");
        var scenario = new string('x', 400);

        var queue = factory.Queue(scenario);
        var topic = factory.Topic(scenario);
        var subscription = factory.Subscription(scenario);

        Assert.Equal(LiveServiceBusNameFactory.QueueAndTopicMaximumLength, queue.Length);
        Assert.Equal(LiveServiceBusNameFactory.QueueAndTopicMaximumLength, topic.Length);
        Assert.Equal(LiveServiceBusNameFactory.SubscriptionMaximumLength, subscription.Length);
        Assert.Matches("-[a-f0-9]{12}$", queue);
        Assert.Matches("-[a-f0-9]{12}$", subscription);
    }

    [Fact]
    public async Task BoundedWait_ReturnsAsSoonAsConditionIsSatisfied()
    {
        var attempts = 0;

        var result = await LiveServiceBusWait.UntilAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult((
                    IsSatisfied: attempts == 2,
                    Value: "ready",
                    Observation: $"attempt={attempts}"));
            },
            "test condition",
            "safe-alias",
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(1));

        Assert.Equal("ready", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task BoundedWait_ReportsSafeTimeoutContext()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            LiveServiceBusWait.UntilAsync(
                _ => Task.FromResult((false, 0, "count=0")),
                "one active message",
                "safe-alias",
                TimeSpan.FromMilliseconds(20),
                TimeSpan.FromMilliseconds(1)));

        Assert.Contains("one active message", exception.Message);
        Assert.Contains("safe-alias", exception.Message);
        Assert.Contains("count=0", exception.Message);
    }

    [Fact]
    public async Task BoundedWait_PropagatesCallerCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LiveServiceBusWait.UntilAsync(
                _ => Task.FromResult((false, 0, "not-ready")),
                "test condition",
                "safe-alias",
                TimeSpan.FromSeconds(5),
                cancellationToken: cancellationSource.Token));
    }
}
