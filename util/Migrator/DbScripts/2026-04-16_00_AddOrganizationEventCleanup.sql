-- OrganizationEventCleanup

-- Table
IF OBJECT_ID('[dbo].[OrganizationEventCleanup]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationEventCleanup] (
        [Id]                 UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
        [QueuedAt]           DATETIME2 (7)    NOT NULL,
        [StartedAt]          DATETIME2 (7)    NULL,
        [LastProgressAt]     DATETIME2 (7)    NULL,
        [CompletedAt]        DATETIME2 (7)    NULL,
        [EventsDeletedCount] BIGINT           NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_EventsDeletedCount] DEFAULT (0),
        [Attempts]           INT              NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_Attempts] DEFAULT (0),
        [LastError]          NVARCHAR(MAX)    NULL,
        CONSTRAINT [PK_OrganizationEventCleanup] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Index
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationEventCleanup_CompletedAt_QueuedAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationEventCleanup_CompletedAt_QueuedAt]
        ON [dbo].[OrganizationEventCleanup]([CompletedAt] ASC, [QueuedAt] ASC);
END
GO

-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @QueuedAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationEventCleanup]
    (
        [Id],
        [OrganizationId],
        [QueuedAt]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @QueuedAt
    )
END
GO

-- Stored Procedures: ReadNextPending
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_ReadNextPending]
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[OrganizationEventCleanup]
    WHERE
        [CompletedAt] IS NULL
    ORDER BY
        [QueuedAt] ASC
END
GO

-- Stored Procedures: MarkStarted
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_MarkStarted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [StartedAt] = COALESCE([StartedAt], SYSUTCDATETIME()),
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: IncrementProgress
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_IncrementProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [EventsDeletedCount] = [EventsDeletedCount] + @Delta,
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: RecordError
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_RecordError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [Attempts] = [Attempts] + 1,
        [LastError] = @Message,
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: MarkCompleted
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_MarkCompleted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [CompletedAt] = SYSUTCDATETIME(),
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
GO
