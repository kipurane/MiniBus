using System.Text.RegularExpressions;

namespace MiniBus.AzureServiceBus.IntegrationTests;

internal static partial class LiveServiceBusDiagnostics
{
    public const string Redacted = "[REDACTED]";

    public static string Redact(string value, params string?[] secrets)
    {
        var redacted = value;
        foreach (var secret in secrets.Where(secret => !string.IsNullOrWhiteSpace(secret)))
        {
            redacted = redacted.Replace(secret!, Redacted, StringComparison.Ordinal);
        }

        return SharedAccessKeyPattern().Replace(redacted, $"SharedAccessKey={Redacted}");
    }

    [GeneratedRegex("SharedAccessKey=[^;\\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex SharedAccessKeyPattern();
}
