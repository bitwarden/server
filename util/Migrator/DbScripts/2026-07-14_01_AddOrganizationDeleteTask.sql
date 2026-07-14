-- OrganizationDeleteTask

-- Table
IF OBJECT_ID('[dbo].[OrganizationDeleteTask]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationDeleteTask] (
        [Id]                 UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
        [TaskType]           TINYINT          NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_TaskType] DEFAULT (0),
        [CreationDate]       DATETIME2(7)     NOT NULL,
        [RevisionDate]       DATETIME2(7)     NOT NULL,
        [StartDate]          DATETIME2(7)     NULL,
        [CompletedDate]      DATETIME2(7)     NULL,
        [ItemsDeletedCount]  BIGINT           NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_ItemsDeletedCount] DEFAULT (0),
        [FailureCount]       INT              NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_FailureCount] DEFAULT (0),
        [LastError]          NVARCHAR(MAX)    NULL,
        CONSTRAINT [PK_OrganizationDeleteTask] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Index
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationDeleteTask_CompletedDate_CreationDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationDeleteTask_CompletedDate_CreationDate]
        ON [dbo].[OrganizationDeleteTask]([CompletedDate] ASC, [CreationDate] ASC);
END
GO


-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDeleteTask_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @TaskType TINYINT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationDeleteTask]
    (
        [Id],
        [OrganizationId],
        [TaskType],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @TaskType,
        @CreationDate,
        @CreationDate
    )
END
GO

-- Stored Procedures: UpdateClaimNextPending
-- Drop the pre-rename proc name if a dev database already ran an earlier version of this migration.
DROP PROCEDURE IF EXISTS [dbo].[OrganizationDeleteTask_ClaimNextPending]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationDeleteTask_UpdateClaimNextPending]
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
            [TaskType],
            [CreationDate],
            [RevisionDate],
            [StartDate],
            [CompletedDate],
            [ItemsDeletedCount],
            [FailureCount],
            [LastError]
        FROM
            [dbo].[OrganizationDeleteTask] WITH (UPDLOCK, READPAST)
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
        inserted.[TaskType],
        inserted.[CreationDate],
        inserted.[RevisionDate],
        inserted.[StartDate],
        inserted.[CompletedDate],
        inserted.[ItemsDeletedCount],
        inserted.[FailureCount],
        inserted.[LastError]
END
GO

-- Stored Procedures: UpdateProgress
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDeleteTask_UpdateProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [ItemsDeletedCount] = [ItemsDeletedCount] + @Delta,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: UpdateError
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDeleteTask_UpdateError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [FailureCount] = [FailureCount] + 1,
        [LastError] = @Message,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: UpdateCompleted
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDeleteTask_UpdateCompleted]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [CompletedDate] = @Now,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
GO
