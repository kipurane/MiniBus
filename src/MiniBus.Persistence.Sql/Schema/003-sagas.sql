IF OBJECT_ID(N'MiniBus.Sagas', N'U') IS NULL
BEGIN
    CREATE TABLE MiniBus.Sagas
    (
        Id uniqueidentifier NOT NULL,
        DataType nvarchar(1024) NOT NULL,
        CorrelationId nvarchar(200) NOT NULL,
        Data varbinary(max) NOT NULL,
        IsCompleted bit NOT NULL CONSTRAINT DF_MiniBus_Sagas_IsCompleted DEFAULT 0,
        CreatedUtc datetimeoffset NOT NULL,
        UpdatedUtc datetimeoffset NOT NULL,
        CompletedUtc datetimeoffset NULL,
        Version rowversion NOT NULL,
        CONSTRAINT PK_MiniBus_Sagas PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_MiniBus_Sagas_DataType_CorrelationId
        ON MiniBus.Sagas (DataType, CorrelationId);

    CREATE INDEX IX_MiniBus_Sagas_Completed
        ON MiniBus.Sagas (IsCompleted, CompletedUtc)
        INCLUDE (DataType, CorrelationId);
END;
