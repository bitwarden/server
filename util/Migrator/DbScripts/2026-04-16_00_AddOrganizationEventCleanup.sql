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

-- Stored Procedures: Organization_DeleteById (queue cleanup row inside the delete transaction)
CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [AP].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [GU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationDomain_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationIntegration_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Project]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[Secret]
    WHERE
        [OrganizationId] = @Id

    DELETE AK
    FROM
        [dbo].[ApiKey] AK
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AK].[ServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AP].[GrantedServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[ServiceAccount]
    WHERE
        [OrganizationId] = @Id

    -- Delete Notification Status
    DELETE
        NS
    FROM
        [dbo].[NotificationStatus] NS
    INNER JOIN
        [dbo].[Notification] N ON N.[Id] = NS.[NotificationId]
    WHERE
        N.[OrganizationId] = @Id

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Application
    DELETE
    FROM
        [dbo].[OrganizationApplication]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Report
    DELETE
    FROM
        [dbo].[OrganizationReport]
    WHERE
        [OrganizationId] = @Id

    -- Queue Organization Event log cleanup (processed asynchronously by background job)
    INSERT INTO [dbo].[OrganizationEventCleanup]
    (
        [Id],
        [OrganizationId],
        [QueuedAt]
    )
    VALUES
    (
        NEWID(),
        @Id,
        SYSUTCDATETIME()
    )

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO
