using MiniBus.Tooling.Sql;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

public sealed class SqlToolingTableNamesTests
{
    [Fact]
    public void Constructor_FormatsDefaultTableNames()
    {
        var tableNames = new SqlToolingTableNames(new MiniBusSqlToolingOptions());

        Assert.Equal("[MiniBus].[Inbox]", tableNames.Inbox);
        Assert.Equal("[MiniBus].[Outbox]", tableNames.Outbox);
        Assert.Equal("[MiniBus].[Sagas]", tableNames.Saga);
    }

    [Fact]
    public void Constructor_EscapesClosingBracketsInIdentifiers()
    {
        var tableNames = new SqlToolingTableNames(new MiniBusSqlToolingOptions
        {
            SchemaName = "schema]name",
            InboxTableName = "inbox]table",
            OutboxTableName = "outbox]table",
            SagaTableName = "saga]table"
        });

        Assert.Equal("[schema]]name].[inbox]]table]", tableNames.Inbox);
        Assert.Equal("[schema]]name].[outbox]]table]", tableNames.Outbox);
        Assert.Equal("[schema]]name].[saga]]table]", tableNames.Saga);
    }

    [Fact]
    public void SchemaScriptRewrite_UsesBracketedIdentifiersForKnownTables()
    {
        var rewritten = SqlServerTestDatabase.RewriteSchemaScript(
            """
            IF SCHEMA_ID(N'MiniBus') IS NULL
                EXEC(N'CREATE SCHEMA MiniBus');
            IF OBJECT_ID(N'MiniBus.Outbox', N'U') IS NULL
                CREATE TABLE MiniBus.Outbox (Id uniqueidentifier NOT NULL);
            CREATE INDEX IX_MiniBus_Outbox_CorrelationId
                ON MiniBus.Outbox (CorrelationId, CreatedUtc);
            """,
            "schema]name");

        Assert.Contains("SCHEMA_ID(N'schema]name')", rewritten, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA [schema]]name]", rewritten, StringComparison.Ordinal);
        Assert.Contains("OBJECT_ID(N'[schema]]name].[Outbox]'", rewritten, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [schema]]name].[Outbox]", rewritten, StringComparison.Ordinal);
        Assert.Contains("ON [schema]]name].[Outbox] (CorrelationId, CreatedUtc)", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("schema]name.Outbox", rewritten, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_RejectsBlankSchemaName(string schemaName)
    {
        var options = new MiniBusSqlToolingOptions
        {
            SchemaName = schemaName
        };

        var exception = Assert.Throws<ArgumentException>(() => new SqlToolingTableNames(options));
        Assert.Equal(nameof(MiniBusSqlToolingOptions.SchemaName), exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_RejectsBlankTableNames(string tableName)
    {
        var inboxOptions = new MiniBusSqlToolingOptions { InboxTableName = tableName };
        var outboxOptions = new MiniBusSqlToolingOptions { OutboxTableName = tableName };
        var sagaOptions = new MiniBusSqlToolingOptions { SagaTableName = tableName };

        Assert.Equal(
            nameof(MiniBusSqlToolingOptions.InboxTableName),
            Assert.Throws<ArgumentException>(() => new SqlToolingTableNames(inboxOptions)).ParamName);
        Assert.Equal(
            nameof(MiniBusSqlToolingOptions.OutboxTableName),
            Assert.Throws<ArgumentException>(() => new SqlToolingTableNames(outboxOptions)).ParamName);
        Assert.Equal(
            nameof(MiniBusSqlToolingOptions.SagaTableName),
            Assert.Throws<ArgumentException>(() => new SqlToolingTableNames(sagaOptions)).ParamName);
    }
}
