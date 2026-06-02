namespace MiniBus.Persistence.Sql;

internal static class SqlSagaSchema
{
    // MiniBus.Sagas.DataType is nvarchar(1024) in Schema/003-sagas.sql.
    public const int DataTypeMaxLength = 1024;

    // MiniBus.Sagas.CorrelationId is nvarchar(200) in Schema/003-sagas.sql.
    public const int CorrelationIdMaxLength = 200;

    // MiniBus.Sagas.Version is SQL Server rowversion (8 bytes) in Schema/003-sagas.sql.
    public const int VersionSize = 8;
}