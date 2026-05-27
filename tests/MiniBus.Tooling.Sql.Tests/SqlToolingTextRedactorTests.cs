using MiniBus.Tooling.Sql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlToolingTextRedactorTests
{
    [Fact]
    public void RedactAndTruncate_TreatsMaxLengthAsHardCap()
    {
        var result = SqlToolingTextRedactor.RedactAndTruncate(
            "This message is longer than the cap.",
            maxLength: 12);

        Assert.Equal(12, result.Length);
        Assert.Equal("This mess...", result);
    }

    [Theory]
    [InlineData(1, ".")]
    [InlineData(2, "..")]
    [InlineData(3, "...")]
    public void RedactAndTruncate_HandlesTinyCaps(int maxLength, string expected)
    {
        var result = SqlToolingTextRedactor.RedactAndTruncate(
            "abcdef",
            maxLength);

        Assert.Equal(expected, result);
        Assert.Equal(maxLength, result.Length);
    }

    [Fact]
    public void RedactAndTruncate_RedactsBeforeTruncating()
    {
        var result = SqlToolingTextRedactor.RedactAndTruncate(
            "Password=super-secret; message",
            maxLength: 100);

        Assert.Contains("Password=<redacted>", result);
        Assert.DoesNotContain("super-secret", result);
    }

    [Fact]
    public void Redact_RedactsSingleLineJsonSecrets()
    {
        var result = SqlToolingTextRedactor.Redact(
            """{"clientSecret":"json-secret","message":"safe"}""");

        Assert.Equal("""{"clientSecret":"<redacted>","message":"safe"}""", result);
    }

    [Fact]
    public void Redact_DoesNotRedactBareKeyFields()
    {
        var result = SqlToolingTextRedactor.Redact(
            """Key=partition-key; ApiKey=api-secret; {"key":"routing-key","accessKey":"access-secret"}""");

        Assert.Contains("Key=partition-key", result);
        Assert.Contains("\"key\":\"routing-key\"", result);
        Assert.Contains("ApiKey=<redacted>", result);
        Assert.Contains("\"accessKey\":\"<redacted>\"", result);
        Assert.DoesNotContain("api-secret", result);
        Assert.DoesNotContain("access-secret", result);
    }

    [Theory]
    [InlineData("Authorization: Bearer abc.def-123_secret", "abc.def-123_secret")]
    [InlineData("authorization: bearer token-with-varied-case", "token-with-varied-case")]
    [InlineData("Authorization: Bearer    token-with-extra-spaces", "token-with-extra-spaces")]
    public void Redact_RedactsBearerTokens(string value, string token)
    {
        var result = SqlToolingTextRedactor.Redact(value);

        Assert.Contains("Bearer <redacted>", result, StringComparison.Ordinal);
        Assert.DoesNotContain(token, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_RedactsQuotedKeyValueSecretsWithSpacesAndCommas()
    {
        var result = SqlToolingTextRedactor.Redact(
            """Login failed. Password="my secret, with comma"; Server='prod sql, primary'; Message=safe""");

        Assert.Contains("Password=<redacted>", result);
        Assert.Contains("Server=<redacted>", result);
        Assert.Contains("Message=safe", result);
        Assert.DoesNotContain("my secret", result);
        Assert.DoesNotContain("prod sql", result);
    }

    [Fact]
    public void Redact_RedactsUnquotedConnectionStringValuesWithSpacesAndCommas()
    {
        var result = SqlToolingTextRedactor.Redact(
            "Password=my secret, with comma;Data Source=tcp://secret.example,1433;Database=MiniBus");

        Assert.Contains("Password=<redacted>", result);
        Assert.Contains("Data Source=<redacted>", result);
        Assert.Contains("Database=MiniBus", result);
        Assert.DoesNotContain("my secret", result);
        Assert.DoesNotContain("secret.example", result);
    }
}
