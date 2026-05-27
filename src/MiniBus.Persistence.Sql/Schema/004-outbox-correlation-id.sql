IF COL_LENGTH(N'MiniBus.Outbox', N'CorrelationId') IS NULL
BEGIN
    ALTER TABLE MiniBus.Outbox
        ADD CorrelationId nvarchar(200) NULL;

    EXEC(N'
    UPDATE MiniBus.Outbox
        SET CorrelationId = LEFT(JSON_VALUE(HeadersJson, ''$."MiniBus.CorrelationId"''), 200)
        WHERE CorrelationId IS NULL
          AND ISJSON(HeadersJson) = 1
          AND JSON_VALUE(HeadersJson, ''$."MiniBus.CorrelationId"'') IS NOT NULL;
    ');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_MiniBus_Outbox_CorrelationId'
      AND object_id = OBJECT_ID(N'MiniBus.Outbox', N'U'))
BEGIN
    CREATE INDEX IX_MiniBus_Outbox_CorrelationId
        ON MiniBus.Outbox (CorrelationId, CreatedUtc);
END;
