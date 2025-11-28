-- Configure FK to cascade on delete
IF EXISTS(SELECT *
FROM information_schema.table_constraints
WHERE table_name='OrganizationIntegrationConfiguration'
    AND constraint_name='FK_OrganizationIntegrationConfiguration_OrganizationIntegration')
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration] DROP FK_OrganizationIntegrationConfiguration_OrganizationIntegration;
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration] ADD CONSTRAINT [FK_OrganizationIntegrationConfiguration_OrganizationIntegration] FOREIGN KEY ([OrganizationIntegrationId]) REFERENCES [dbo].[OrganizationIntegration] ([Id]) ON DELETE CASCADE;
END
GO

-- New procedures for CRUD on OrganizationIntegration and OrganizationIntegrationConfiguration
CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegration]
        (
        [Id],
        [OrganizationId],
        [Type],
        [Configuration],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Type,
            @Configuration,
            @CreationDate,
            @RevisionDate
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegrationConfiguration]
        (
        [Id],
        [OrganizationIntegrationId],
        [EventType],
        [Configuration],
        [Template],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationIntegrationId,
            @EventType,
            @Configuration,
            @Template,
            @CreationDate,
            @RevisionDate
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Configuration] = @Configuration,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [OrganizationIntegrationId] = @OrganizationIntegrationId,
        [EventType] = @EventType,
        [Configuration] = @Configuration,
        [Template] = @Template,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
GO

-- Organization cleanup
CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH
    RECOMPILE
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

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
