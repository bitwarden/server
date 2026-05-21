-- OrganizationEventCleanup

-- Table
IF OBJECT_ID('[dbo].[OrganizationEventCleanup]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationEventCleanup] (
        [Id]                 UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
        [CreationDate]       DATETIME2 (7)    NOT NULL,
        [RevisionDate]       DATETIME2 (7)    NULL,
        [StartDate]          DATETIME2 (7)    NULL,
        [CompletedDate]      DATETIME2 (7)    NULL,
        [EventsDeletedCount] BIGINT           NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_EventsDeletedCount] DEFAULT (0),
        [FailureCount]       INT              NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_FailureCount] DEFAULT (0),
        [LastError]          NVARCHAR(MAX)    NULL,
        CONSTRAINT [PK_OrganizationEventCleanup] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Index
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationEventCleanup_CompletedDate_CreationDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationEventCleanup_CompletedDate_CreationDate]
        ON [dbo].[OrganizationEventCleanup]([CompletedDate] ASC, [CreationDate] ASC);
END
GO


-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationEventCleanup]
    (
        [Id],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CreationDate
    )
END
GO

-- Stored Procedures: ClaimNextPending
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_ClaimNextPending]
    @Now DATETIME2(7),
    @StaleLeaseThreshold DATETIME2(7),
    @MaxFailureCount INT
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [Pending] AS (
        SELECT TOP 1
            [Id],
            [OrganizationId],
            [CreationDate],
            [RevisionDate],
            [StartDate],
            [CompletedDate],
            [EventsDeletedCount],
            [FailureCount],
            [LastError]
        FROM
            [dbo].[OrganizationEventCleanup] WITH (UPDLOCK, READPAST)
        WHERE
            [CompletedDate] IS NULL
            AND ([StartDate] IS NULL OR [RevisionDate] < @StaleLeaseThreshold)
            AND [FailureCount] < @MaxFailureCount
        ORDER BY
            [CreationDate] ASC
    )
    UPDATE [Pending]
    SET
        [StartDate] = COALESCE([StartDate], @Now),
        [RevisionDate] = @Now
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[CreationDate],
        inserted.[RevisionDate],
        inserted.[StartDate],
        inserted.[CompletedDate],
        inserted.[EventsDeletedCount],
        inserted.[FailureCount],
        inserted.[LastError]
END
GO

-- Stored Procedures: UpdateProgress
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_UpdateProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [EventsDeletedCount] = [EventsDeletedCount] + @Delta,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: UpdateError
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_UpdateError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [FailureCount] = [FailureCount] + 1,
        [LastError] = @Message,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: UpdateCompleted
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_UpdateCompleted]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [CompletedDate] = @Now,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO
