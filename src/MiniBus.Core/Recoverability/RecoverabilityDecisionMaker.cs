using System.Collections.ObjectModel;

namespace MiniBus.Core.Recoverability;

public sealed class RecoverabilityDecisionMaker
{
    public const string RetriesExhaustedDeadLetterReason = "MiniBus retries exhausted";
    private const int MaxDeadLetterDescriptionLength = 4096;

    public RecoverabilityDecision Decide(
        IReadOnlyDictionary<string, string> headers,
        MiniBusRecoverabilityOptions options,
        Exception exception,
        string receivedMessageId)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exception);

        if (options.ImmediateRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Immediate retries cannot be negative.");
        }

        var currentImmediateAttempt = ReadInt32(headers, MiniBusRecoverabilityHeaderNames.ImmediateAttempt);
        var currentDelayedAttempt = ReadInt32(headers, MiniBusRecoverabilityHeaderNames.DelayedAttempt);

        if (currentImmediateAttempt < options.ImmediateRetries)
        {
            var nextImmediateAttempt = currentImmediateAttempt + 1;
            var retryHeaders = CreateHeaders(
                headers,
                options,
                exception,
                receivedMessageId,
                nextImmediateAttempt,
                currentDelayedAttempt);

            return new RecoverabilityDecision(
                RecoverabilityDecisionKind.ImmediateRetry,
                retryHeaders,
                exception,
                nextImmediateAttempt,
                currentDelayedAttempt);
        }

        if (currentDelayedAttempt < options.DelayedRetries.Count)
        {
            var nextDelayedAttempt = currentDelayedAttempt + 1;
            var retryHeaders = CreateHeaders(
                headers,
                options,
                exception,
                receivedMessageId,
                immediateAttempt: 0,
                delayedAttempt: nextDelayedAttempt);

            return new RecoverabilityDecision(
                RecoverabilityDecisionKind.DelayedRetry,
                retryHeaders,
                exception,
                ImmediateAttempt: 0,
                DelayedAttempt: nextDelayedAttempt,
                Delay: options.DelayedRetries[currentDelayedAttempt]);
        }

        var exhaustedHeaders = CreateHeaders(
            headers,
            options,
            exception,
            receivedMessageId,
            currentImmediateAttempt,
            currentDelayedAttempt);

        if (!options.DeadLetterAfterRetriesExhausted)
        {
            return new RecoverabilityDecision(
                RecoverabilityDecisionKind.Propagate,
                exhaustedHeaders,
                exception,
                currentImmediateAttempt,
                currentDelayedAttempt);
        }

        return new RecoverabilityDecision(
            RecoverabilityDecisionKind.DeadLetter,
            exhaustedHeaders,
            exception,
            currentImmediateAttempt,
            currentDelayedAttempt,
            DeadLetterReason: RetriesExhaustedDeadLetterReason,
            DeadLetterDescription: CreateDeadLetterDescription(
                exhaustedHeaders,
                options,
                exception,
                currentImmediateAttempt,
                currentDelayedAttempt));
    }

    private static IReadOnlyDictionary<string, string> CreateHeaders(
        IReadOnlyDictionary<string, string> headers,
        MiniBusRecoverabilityOptions options,
        Exception exception,
        string receivedMessageId,
        int immediateAttempt,
        int delayedAttempt)
    {
        var updatedHeaders = new Dictionary<string, string>(headers, StringComparer.Ordinal)
        {
            [MiniBusRecoverabilityHeaderNames.ImmediateAttempt] = immediateAttempt.ToString(),
            [MiniBusRecoverabilityHeaderNames.DelayedAttempt] = delayedAttempt.ToString(),
            [MiniBusRecoverabilityHeaderNames.MaxImmediateAttempts] = options.ImmediateRetries.ToString(),
            [MiniBusRecoverabilityHeaderNames.MaxDelayedAttempts] = options.DelayedRetries.Count.ToString(),
            [MiniBusRecoverabilityHeaderNames.ExceptionType] = exception.GetType().FullName ?? exception.GetType().Name,
            [MiniBusRecoverabilityHeaderNames.ExceptionMessage] = exception.Message
        };

        if (!updatedHeaders.ContainsKey(MiniBusRecoverabilityHeaderNames.OriginalMessageId)
            && !string.IsNullOrWhiteSpace(receivedMessageId))
        {
            updatedHeaders[MiniBusRecoverabilityHeaderNames.OriginalMessageId] = receivedMessageId;
        }

        return new ReadOnlyDictionary<string, string>(updatedHeaders);
    }

    private static string CreateDeadLetterDescription(
        IReadOnlyDictionary<string, string> headers,
        MiniBusRecoverabilityOptions options,
        Exception exception,
        int immediateAttempt,
        int delayedAttempt)
    {
        headers.TryGetValue(MiniBusRecoverabilityHeaderNames.OriginalMessageId, out var originalMessageId);

        var description = string.Join(
            "; ",
            $"ExceptionType={exception.GetType().FullName ?? exception.GetType().Name}",
            $"ExceptionMessage={exception.Message}",
            $"ImmediateAttempt={immediateAttempt}",
            $"DelayedAttempt={delayedAttempt}",
            $"MaxImmediateAttempts={options.ImmediateRetries}",
            $"MaxDelayedAttempts={options.DelayedRetries.Count}",
            $"OriginalMessageId={originalMessageId ?? string.Empty}");

        return description.Length <= MaxDeadLetterDescriptionLength
            ? description
            : description[..MaxDeadLetterDescriptionLength];
    }

    private static int ReadInt32(IReadOnlyDictionary<string, string> headers, string headerName)
    {
        return headers.TryGetValue(headerName, out var value) && int.TryParse(value, out var parsedValue)
            ? parsedValue
            : 0;
    }
}
