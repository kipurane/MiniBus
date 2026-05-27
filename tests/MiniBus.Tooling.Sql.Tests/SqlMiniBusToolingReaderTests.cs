using MiniBus.Tooling.Core;
using MiniBus.Tooling.Sql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlMiniBusToolingReaderTests
{
    [Fact]
    public void Constructor_RejectsNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new SqlMiniBusToolingReader(null!));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDefaultQueryLimit()
    {
        var options = new MiniBusSqlToolingOptions
        {
            ConnectionString = "Server=localhost;Database=MiniBus;",
            DefaultQueryLimit = 0
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SqlMiniBusToolingReader(options));

        Assert.Equal(nameof(MiniBusSqlToolingOptions.DefaultQueryLimit), exception.ParamName);
        Assert.Contains("query limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Options_CreateConnectionRejectsNullFactoryResult()
    {
        var options = new MiniBusSqlToolingOptions
        {
            ConnectionFactory = () => null!
        };

        var exception = Assert.Throws<InvalidOperationException>(
            options.CreateConnection);

        Assert.Contains("connection factory returned null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InboxReader_InvalidFilterThrowsArgumentException()
    {
        var reader = CreateReaderWithoutConnection();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            reader.ListAsync(CreateInvalidFilter()));

        Assert.Equal("filter", exception.ParamName);
    }

    [Fact]
    public async Task OutboxReader_InvalidFilterThrowsArgumentException()
    {
        var reader = CreateReaderWithoutConnection();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IMiniBusOutboxToolingReader)reader).ListAsync(CreateInvalidFilter()));

        Assert.Equal("filter", exception.ParamName);
    }

    [Fact]
    public async Task SagaReader_InvalidFilterThrowsArgumentException()
    {
        var reader = CreateReaderWithoutConnection();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IMiniBusSagaToolingReader)reader).ListAsync(CreateInvalidFilter()));

        Assert.Equal("filter", exception.ParamName);
    }

    private static SqlMiniBusToolingReader CreateReaderWithoutConnection()
    {
        return new SqlMiniBusToolingReader(new MiniBusSqlToolingOptions
        {
            ConnectionFactory = () => throw new InvalidOperationException("The reader should validate filters before opening a connection.")
        });
    }

    private static MiniBusToolingQueryFilter CreateInvalidFilter()
    {
        return new MiniBusToolingQueryFilter
        {
            FromUtc = new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 5, 25, 11, 0, 0, TimeSpan.Zero)
        };
    }
}
