IF SCHEMA_ID(N'MiniBus') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA MiniBus');
END;

IF OBJECT_ID(N'MiniBus.Inbox', N'U') IS NULL
BEGIN
    CREATE TABLE MiniBus.Inbox
    (
        EndpointName nvarchar(200) NOT NULL,
        MessageId nvarchar(200) NOT NULL,
        ProcessedUtc datetimeoffset NOT NULL,
        HeadersJson nvarchar(max) NOT NULL,
        CorrelationId nvarchar(200) NULL,
        CONSTRAINT PK_MiniBus_Inbox PRIMARY KEY (EndpointName, MessageId)
    );
END;

IF OBJECT_ID(N'MiniBus.Outbox', N'U') IS NULL
BEGIN
    CREATE TABLE MiniBus.Outbox
    (
        Id uniqueidentifier NOT NULL,
        OutgoingMessageId nvarchar(200) NOT NULL,
        EndpointName nvarchar(200) NOT NULL,
        IncomingMessageId nvarchar(200) NOT NULL,
        OperationKind nvarchar(32) NOT NULL,
        MessageType nvarchar(1024) NOT NULL,
        Body varbinary(max) NOT NULL,
        HeadersJson nvarchar(max) NOT NULL,
        CorrelationId nvarchar(200) NULL,
        DueTime datetimeoffset NULL,
        CreatedUtc datetimeoffset NOT NULL,
        ClaimedUtc datetimeoffset NULL,
        DispatchedUtc datetimeoffset NULL,
        AttemptCount int NOT NULL CONSTRAINT DF_MiniBus_Outbox_AttemptCount DEFAULT 0,
        LastError nvarchar(max) NULL,
        CONSTRAINT PK_MiniBus_Outbox PRIMARY KEY (Id)
    );

    CREATE INDEX IX_MiniBus_Outbox_Pending
        ON MiniBus.Outbox (DispatchedUtc, ClaimedUtc, CreatedUtc)
        INCLUDE (AttemptCount, DueTime);

    CREATE INDEX IX_MiniBus_Outbox_IncomingMessage
        ON MiniBus.Outbox (EndpointName, IncomingMessageId);

    CREATE INDEX IX_MiniBus_Outbox_CorrelationId
        ON MiniBus.Outbox (CorrelationId, CreatedUtc);

    CREATE UNIQUE INDEX UX_MiniBus_Outbox_OutgoingMessageId
        ON MiniBus.Outbox (OutgoingMessageId);
END;
