using System.Text.RegularExpressions;

namespace MiniBus.Tooling.Sql;

internal static class SqlToolingTextRedactor
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex BearerTokenRegex = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexMatchTimeout);

    private static readonly Regex SensitiveJsonValueRegex = new(
        @"(""(?:authorization|password|pwd|sharedaccesskey|accountkey|accesskey|apikey|secret|clientsecret|sastoken|signature|sig)""\s*:\s*"")[^""]*("")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexMatchTimeout);

    private const string SensitiveKeyPattern =
        @"Authorization|Password|Pwd|SharedAccessKey|AccountKey|AccessKey|ApiKey|Secret|ClientSecret|SasToken|Signature|sig|Server|Data\s+Source|Host|User\s+Id|UserId|Uid";

    private static readonly Regex SensitiveQuotedKeyValueRegex = new(
        @$"\b(?<key>{SensitiveKeyPattern})\s*=\s*(?:""[^""]*""|'[^']*')",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexMatchTimeout);

    private static readonly Regex SensitiveUnquotedKeyValueRegex = new(
        @$"\b(?<key>{SensitiveKeyPattern})\s*=\s*[^;\r\n{{}}]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        RegexMatchTimeout);

    public static string Redact(string value)
    {
        var redacted = BearerTokenRegex.Replace(value, "Bearer <redacted>");
        redacted = SensitiveJsonValueRegex.Replace(redacted, "$1<redacted>$2");
        redacted = SensitiveQuotedKeyValueRegex.Replace(redacted, "${key}=<redacted>");
        return SensitiveUnquotedKeyValueRegex.Replace(redacted, "${key}=<redacted>");
    }

    public static string RedactAndTruncate(string value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var redacted = Redact(value);
        if (redacted.Length <= maxLength)
        {
            return redacted;
        }

        const string ellipsis = "...";
        if (maxLength <= ellipsis.Length)
        {
            return ellipsis[..maxLength];
        }

        return string.Concat(redacted.AsSpan(0, maxLength - ellipsis.Length), ellipsis);
    }
}
