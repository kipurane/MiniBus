namespace MiniBus.Tooling.Sql.Tests;

internal static class SqlServerTestSettings
{
    public const string ConnectionStringEnvironmentVariable = "MINIBUS_SQLSERVER_TEST_CONNECTION_STRING";
    public const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
}
