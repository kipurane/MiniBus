namespace MiniBus.Persistence.Sql;

public sealed record SerializedSagaData(
    string DataType,
    byte[] Body);
