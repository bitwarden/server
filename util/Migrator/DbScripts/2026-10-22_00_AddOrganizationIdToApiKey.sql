IF COL_LENGTH('[dbo].[ApiKey]', 'OrganizationId') IS NULL
BEGIN
    ALTER TABLE [dbo].[ApiKey]
        ADD [OrganizationId] UNIQUEIDENTIFIER NULL;
END
GO

IF COLUMNPROPERTY(OBJECT_ID('[dbo].[ApiKey]'), 'EncryptedPayload', 'AllowsNull') = 0
BEGIN
    ALTER TABLE [dbo].[ApiKey]
        ALTER COLUMN [EncryptedPayload] NVARCHAR(4000) NULL;
END
GO

IF COLUMNPROPERTY(OBJECT_ID('[dbo].[ApiKey]'), 'Key', 'AllowsNull') = 0
BEGIN
    ALTER TABLE [dbo].[ApiKey]
        ALTER COLUMN [Key] VARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_ApiKey_OrganizationId'
      AND [parent_object_id] = OBJECT_ID('[dbo].[ApiKey]')
)
BEGIN
    ALTER TABLE [dbo].[ApiKey]
        ADD CONSTRAINT [FK_ApiKey_OrganizationId] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ApiKey_OrganizationId'
      AND [object_id] = OBJECT_ID('[dbo].[ApiKey]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ApiKey_OrganizationId]
        ON [dbo].[ApiKey]([OrganizationId] ASC);
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[dbo].[ApiKey]')
      AND [name] = 'Name'
      AND TYPE_NAME([system_type_id]) <> 'nvarchar'
)
BEGIN
    ALTER TABLE [dbo].[ApiKey]
        ALTER COLUMN [Name] NVARCHAR(200) NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = 'CK_ApiKey_SingleOwner'
      AND [parent_object_id] = OBJECT_ID('[dbo].[ApiKey]')
)
BEGIN
    ALTER TABLE [dbo].[ApiKey] WITH CHECK
        ADD CONSTRAINT [CK_ApiKey_SingleOwner] CHECK ([ServiceAccountId] IS NULL OR [OrganizationId] IS NULL);
END
GO

EXEC sp_refreshview N'[dbo].[ApiKeyView]';
GO

EXEC sp_refreshview N'[dbo].[ApiKeyDetailsView]';
GO

EXEC sp_refreshsqlmodule N'[dbo].[ApiKey_ReadByServiceAccountId]';
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ServiceAccountId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @ClientSecretHash VARCHAR(128),
    @Scope NVARCHAR(4000),
    @EncryptedPayload NVARCHAR(4000),
    @Key VARCHAR(MAX),
    @ExpireAt DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @OrganizationId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ApiKey]
    (
        [Id],
        [ServiceAccountId],
        [Name],
        [ClientSecretHash],
        [Scope],
        [EncryptedPayload],
        [Key],
        [ExpireAt],
        [CreationDate],
        [RevisionDate],
        [OrganizationId]
    )
    VALUES
    (
        @Id,
        @ServiceAccountId,
        @Name,
        @ClientSecretHash,
        @Scope,
        @EncryptedPayload,
        @Key,
        @ExpireAt,
        @CreationDate,
        @RevisionDate,
        @OrganizationId
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ApiKey]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKeyDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyDetailsView]
    WHERE
        [Id] = @Id
        AND [ServiceAccountId] IS NOT NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
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
    EXEC [dbo].[ApiKey_OrganizationDeleted] @Id

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
