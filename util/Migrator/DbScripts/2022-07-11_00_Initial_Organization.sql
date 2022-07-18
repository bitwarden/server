IF OBJECT_ID('[dbo].[OrganizationPasswordManager]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrganizationPasswordManager](
        [Id] [uniqueidentifier] NOT NULL,
        [OrganizationId] [uniqueidentifier] NOT NULL,
        [Plan] [nvarchar](50) NOT NULL,
        [PlanType] [tinyint] NOT NULL,
        [Seats] [int] NULL,
        [MaxCollections] [smallint] NULL,
        [UseTotp] [bit] NULL,
        [UsersGetPremium] [bit] NOT NULL,
        [Storage] [bigint] NULL,
        [MaxStorageGb] [smallint] NULL,
        [MaxAutoscaleSeats] [int] NULL,
        [RevisionDate] DATETIME NULL,
        CONSTRAINT [PK_OrganizationPasswordManager] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationPasswordManager_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    ) 
END
GO

IF OBJECT_ID('[dbo].[OrganizationSecretsManager]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrganizationSecretsManager](
        [Id] [uniqueidentifier] NOT NULL,
        [OrganizationId] [uniqueidentifier] NOT NULL,
        [Plan] [nvarchar](50) NOT NULL,
        [PlanType] [tinyint] NOT NULL,
        [UserSeats] [int] NULL,
        [ServiceAccountSeats] [int] NULL,
        [UseEnvironments] [bit] NULL,
        [MaxAutoscaleUserSeats] [int] NULL,
        [MaxAutoscaleServiceAccounts] [int] NULL,
        [MaxProjects] [int] NULL,
        [RevisionDate] DATETIME NULL,
        CONSTRAINT [PK_OrganizationSecretsManager] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationSecretsManager_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    ) 
END
GO

IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
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

    DELETE
    FROM
        [dbo].[OrganizationPasswordManager]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationSecretsManager]
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

IF EXISTS (
 SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'Plan' AND
        DATA_TYPE = 'NVARCHAR' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN [Plan] NVARCHAR(50) NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'PlanType' AND
        DATA_TYPE = 'tinyint' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN PlanType tinyint NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'UseTotp' AND
        DATA_TYPE = 'bit' AND
        TABLE_NAME = 'Organization' AND
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN UseTotp bit NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'UsersGetPremium' AND
        DATA_TYPE = 'bit' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN UsersGetPremium bit NULL
END
GO

IF OBJECT_ID('[dbo].[OrganizationPasswordManager_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationPasswordManager_Update]
END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Update]
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan [nvarchar](50),
    @PlanType [tinyint],
    @Seats [int],
    @MaxCollections [smallint],
    @UseTotp [bit],
    @UsersGetPremium [bit],
    @Storage [bigint],
    @MaxStorageGb [smallint],
    @MaxAutoscaleSeats [int]
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPasswordManager]
    SET
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UseTotp] = @UseTotp,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[OrganizationSecretsManager_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSecretsManager_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSecretsManager_Update]
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan [nvarchar](50),
    @PlanType [tinyint],
    @UserSeats [int],
    @ServiceAccountSeats [int],
    @UseEnvironments [bit],
    @MaxAutoscaleUserSeats [int],
    @MaxAutoscaleServiceAccounts [int],
    @MaxProjects [int]
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSecretsManager]
    SET
        [OrganizationId] = @OrganizationId,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [UserSeats] = @UserSeats,
        [ServiceAccountSeats] = @ServiceAccountSeats,
        [UseEnvironments] = @UseEnvironments,
        [MaxAutoscaleUserSeats] = @MaxAutoscaleUserSeats,
        [MaxAutoscaleServiceAccounts] = @MaxAutoscaleServiceAccounts,
        [MaxProjects] = @MaxProjects,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Organization_ReadAbilities]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadAbilities]
END
GO

CREATE PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.[Id],
        O.[UseEvents],
        O.[Use2fa],
        CASE
            WHEN [Use2fa] = 1 AND [TwoFactorProviders] IS NOT NULL AND [TwoFactorProviders] != '{}' THEN
                1
            ELSE
                0
            END AS [Using2fa],
        ISNULL(OPM.UsersGetPremium, O.[UsersGetPremium]) AS UsersGetPremium,
        O.[UseSso],
        O.[UseKeyConnector],
        O.[UseResetPassword],
        O.[Enabled]
    FROM
         [dbo].[Organization] O
    LEFT JOIN OrganizationPasswordManager OPM on OPM.OrganizationId = O.Id
END
GO


IF OBJECT_ID('[dbo].[OrganizationPasswordManager_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationPasswordManager_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[OrganizationPasswordManager_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentStorage BIGINT
    DECLARE @SendStorage BIGINT

    CREATE TABLE #OrgStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #OrgStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #OrgStorageUpdateTemp
    )
    SELECT
        @AttachmentStorage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #OrgStorageUpdateTemp

    ;WITH [CTE] AS (
        SELECT
            [Id],
            CAST(JSON_VALUE([Data],'$.Size') AS BIGINT) [Size]
        FROM
            [Send]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id
    )
    SELECT
        @SendStorage = SUM([CTE].[Size])
    FROM
        [CTE]

    UPDATE
        [dbo].[OrganizationPasswordManager]
    SET
        [Storage] = (ISNULL(@AttachmentStorage, 0) + ISNULL(@SendStorage, 0)),
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
 

IF OBJECT_ID('[dbo].[OrganizationPasswordManager_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationPasswordManager_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats INT,
    @UseTotp BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @MaxAutoscaleSeats INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPasswordManager]
    (
        [Id],
        [OrganizationId],
        [Plan],
        [PlanType],
        [Seats],
        [UseTotp],
        [UsersGetPremium],
        [Storage],
        [MaxStorageGb],
        [MaxAutoscaleSeats],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Plan,
        @PlanType,
        @Seats,
        @UseTotp,
        @UsersGetPremium,
        @Storage,
        @MaxStorageGb,
        @MaxAutoscaleSeats,
        GETUTCDATE()
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationSecretManager_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSecretManager_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSecretManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @UserSeats INT,
    @ServiceAccountSeats INT,
    @UseEnvironments BIT,
    @NaxAutoscaleUserSeats INT,
    @MaxAutoScaleServiceAccounts INT,
    @MaxProjects INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSecretManager]
    (
        [Id],
        [OrganizationId],
        [Plan],
        [PlanType],
        [UserSeats],
        [ServiceAccountSeats],
        [UseEnvironments],
        [NaxAutoscaleUserSeats],
        [MaxAutoScaleServiceAccounts],
        [MaxProjects],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Plan,
        @PlanType,
        @UserSeats,
        @ServiceAccountSeats,
        @UseEnvironments,
        @NaxAutoscaleUserSeats,
        @MaxAutoScaleServiceAccounts,
        @MaxProjects,
        GETUTCDATE()
    )
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationView')
BEGIN
    DROP VIEW [dbo].[OrganizationView];
END
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    O.[Id],
    O.[Identifier],
    O.[Name],
    O.[BusinessName],
    O.[BusinessAddress1],
    O.[BusinessAddress2],
    O.[BusinessAddress3],
    O.[BusinessCountry],
    O.[BusinessTaxNumber],
    O.[BillingEmail],
    O.[UsePolicies],
    O.[UseSso],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[Use2fA],
    O.[UseApi],
    O.[UseResetPassword],
    O.[SelfHost],
    O.[Gateway],
    O.[GatewayCustomerId],
    O.[GatewaySubscriptionId],
    O.[ReferenceData],
    O.[Enabled],
    O.[LicenseKey],
    O.[ApiKey],
    O.[PublicKey],
    O.[PrivateKey],
    O.[TwoFactorProviders],
    O.[ExpirationDate],
    O.[CreationDate],
    O.[RevisionDate],
    O.[OwnersNotifiedOfAutoscaling],
    O.[UseKeyConnector],
    ISNULL(OPM.[MaxAutoScaleSeats], O.[MaxAutoscaleSeats]) As MaxAutoScaleSeats,
    ISNULL(OPM.[UsersGetPremium], O.[UsersGetPremium]) As UsersGetPremium,
    ISNULL(OPM.[Storage], O.[Storage]) As Storage,
    ISNULL(OPM.[MaxStorageGb], O.[MaxStorageGb]) As MaxStorageGb,
    ISNULL(OPM.[UseTotp], O.[UseTotp]) As UseTotp,
    ISNULL(OPM.[Plan], O.[Plan]) As [Plan],
    ISNULL(OPM.[PlanType], O.[PlanType]) As PlanType,
    ISNULL(OPM.[Seats], O.[Seats]) As Seats,
    ISNULL(OPM.[MaxCollections], O.[MaxCollections]) As MaxCollections
FROM
    [dbo].[Organization] O
    LEFT JOIN OrganizationPasswordManager OPM on OPM.OrganizationId = O.Id
GO

