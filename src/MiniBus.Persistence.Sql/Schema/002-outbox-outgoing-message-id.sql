IF COL_LENGTH(N'MiniBus.Outbox', N'OutgoingMessageId') IS NULL
BEGIN
    ALTER TABLE MiniBus.Outbox
        ADD OutgoingMessageId nvarchar(200) NULL;

    -- Existing rows predate deterministic outbox ids, and the original capture sequence cannot be
    -- reconstructed reliably from persisted data. Backfill with the row id so pending legacy rows
    -- keep a stable replay id after migration; drain or manually clean old pending rows before
    -- applying this script if deterministic ids are required for them too.
    UPDATE MiniBus.Outbox
        SET OutgoingMessageId = CONVERT(nvarchar(36), Id)
        WHERE OutgoingMessageId IS NULL;

    ALTER TABLE MiniBus.Outbox
        ALTER COLUMN OutgoingMessageId nvarchar(200) NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_MiniBus_Outbox_OutgoingMessageId'
      AND object_id = OBJECT_ID(N'MiniBus.Outbox', N'U'))
BEGIN
    CREATE UNIQUE INDEX UX_MiniBus_Outbox_OutgoingMessageId
        ON MiniBus.Outbox (OutgoingMessageId);
END;
