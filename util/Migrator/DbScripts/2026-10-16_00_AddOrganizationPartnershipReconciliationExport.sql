IF OBJECT_ID('[dbo].[OrganizationPartnershipReconciliationExport]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationPartnershipReconciliationExport]
    (
        [Id]                        UNIQUEIDENTIFIER NOT NULL,
        [OrganizationPartnershipId] UNIQUEIDENTIFIER NOT NULL,
        [Status]                    TINYINT          NOT NULL,
        [RecordCount]               INT              NULL,
        [BlobName]                  NVARCHAR(256)    NULL,
        [CreationDate]              DATETIME2(7)     NOT NULL,
        [CompletionDate]            DATETIME2(7)     NULL,
        [ExpirationDate]            DATETIME2(7)     NOT NULL,
        [RevisionDate]              DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_OrganizationPartnershipReconciliationExport] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationPartnershipReconciliationExport_OrganizationPartnership] FOREIGN KEY ([OrganizationPartnershipId]) REFERENCES [dbo].[OrganizationPartnership] ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipReconciliationExport_OrganizationPartnershipId' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipReconciliationExport]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_OrganizationPartnershipId]
        ON [dbo].[OrganizationPartnershipReconciliationExport]([OrganizationPartnershipId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipReconciliationExport_Status' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipReconciliationExport]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_Status]
        ON [dbo].[OrganizationPartnershipReconciliationExport]([Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipReconciliationExport_ExpirationDate' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipReconciliationExport]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_ExpirationDate]
        ON [dbo].[OrganizationPartnershipReconciliationExport]([ExpirationDate] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId]
        ON [dbo].[OrganizationPartnershipEntitlement]([OrganizationPartnershipId] ASC);
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationPartnershipReconciliationExportView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationPartnershipReconciliationExport]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_Create]
    @Id                        UNIQUEIDENTIFIER OUTPUT,
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @Status                    TINYINT,
    @RecordCount               INT,
    @BlobName                  NVARCHAR(256),
    @CreationDate              DATETIME2(7),
    @CompletionDate            DATETIME2(7),
    @ExpirationDate            DATETIME2(7),
    @RevisionDate              DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnershipReconciliationExport]
    (
        [Id],
        [OrganizationPartnershipId],
        [Status],
        [RecordCount],
        [BlobName],
        [CreationDate],
        [CompletionDate],
        [ExpirationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationPartnershipId,
        @Status,
        @RecordCount,
        @BlobName,
        @CreationDate,
        @CompletionDate,
        @ExpirationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadManyPending]
AS
BEGIN
    SET NOCOUNT ON

    -- Status 0 is PartnershipReconciliationExportStatus.Pending
    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [Status] = 0
    ORDER BY
        [CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadManyExpired]
    @AsOf DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [ExpirationDate] <= @AsOf
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_StartIfPending]
    @Id           UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Status 0 is Pending and 1 is Running
    UPDATE
        [dbo].[OrganizationPartnershipReconciliationExport]
    SET
        [Status] = 1,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [Status] = 0

    SELECT @@ROWCOUNT
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_Update]
    @Id                        UNIQUEIDENTIFIER,
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @Status                    TINYINT,
    @RecordCount               INT,
    @BlobName                  NVARCHAR(256),
    @CreationDate              DATETIME2(7),
    @CompletionDate            DATETIME2(7),
    @ExpirationDate            DATETIME2(7),
    @RevisionDate              DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPartnershipReconciliationExport]
    SET
        [OrganizationPartnershipId] = @OrganizationPartnershipId,
        [Status] = @Status,
        [RecordCount] = @RecordCount,
        [BlobName] = @BlobName,
        [CreationDate] = @CreationDate,
        [CompletionDate] = @CompletionDate,
        [ExpirationDate] = @ExpirationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPartnershipReconciliationExport]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadPageByOrganizationPartnershipId]
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @AfterId                   UNIQUEIDENTIFIER,
    @PageSize                  INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@PageSize)
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [OrganizationPartnershipId] = @OrganizationPartnershipId
        AND (@AfterId IS NULL OR [Id] > @AfterId)
    ORDER BY
        [Id] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION OrgPartnership_DeleteById

    DELETE
    FROM
        [dbo].[OrganizationPartnershipReconciliationExport]
    WHERE
        [OrganizationPartnershipId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationPartnershipEntitlement]
    WHERE
        [OrganizationPartnershipId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationPartnership]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION OrgPartnership_DeleteById
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @OrganizationDeleteTasks NVARCHAR(MAX) = NULL
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

    -- Delete Organization Owned Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Partnership Reconciliation Exports
    DELETE
        OPRE
    FROM
        [dbo].[OrganizationPartnershipReconciliationExport] OPRE
    INNER JOIN
        [dbo].[OrganizationPartnership] OP ON OP.[Id] = OPRE.[OrganizationPartnershipId]
    WHERE
        OP.[OrganizationId] = @Id

    -- Delete Organization Partnership Entitlements
    DELETE
        OPE
    FROM
        [dbo].[OrganizationPartnershipEntitlement] OPE
    INNER JOIN
        [dbo].[OrganizationPartnership] OP ON OP.[Id] = OPE.[OrganizationPartnershipId]
    WHERE
        OP.[OrganizationId] = @Id

    -- Delete Organization Partnership
    DELETE
    FROM
        [dbo].[OrganizationPartnership]
    WHERE
        [OrganizationId] = @Id

    -- Atomically enqueue one or more OrganizationDeleteTasks (e.g. for purging Table
    -- Storage event logs) so downstream cleanup is durably recorded with the deletion.
    -- Tasks are passed as a JSON array of { Id, TaskType, CreationDate } objects, letting
    -- any number of teams enqueue their own cleanup type in the same transaction as the delete.
    IF @OrganizationDeleteTasks IS NOT NULL
    BEGIN
        -- [TaskType] and [CreationDate] are read straight from OPENJSON without the ISNULL
        -- fallbacks that bulk JSON procedures normally need. That guidance covers adding a
        -- NOT NULL column to an existing table, where an older server omits the new field and
        -- OPENJSON yields NULL. Here the parameter, the JSON contract, and the table all ship
        -- together: an older server sends no JSON at all, so the guard above skips the insert.
        -- An explicit NULL [TaskType] should fail rather than silently enqueue task type 0.
        INSERT INTO [dbo].[OrganizationDeleteTask]
        (
            [Id],
            [OrganizationId],
            [TaskType],
            [CreationDate],
            [RevisionDate]
        )
        SELECT
            [Id],
            @Id,
            [TaskType],
            [CreationDate],
            [CreationDate]
        FROM
            OPENJSON(@OrganizationDeleteTasks)
            WITH (
                [Id]           UNIQUEIDENTIFIER '$.Id',
                [TaskType]     TINYINT          '$.TaskType',
                [CreationDate] DATETIME2(7)     '$.CreationDate'
            )
    END

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO
