IF OBJECT_ID('[dbo].[OrganizationPasswordManager]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrganizationPasswordManager]
END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
    [RevisionDate] DATETIME NULL
) 
GO

IF OBJECT_ID('[dbo].[OrganizationSecretsManager]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrganizationSecretsManager]
END
GO

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
    [RevisionDate] DATETIME NULL
) 
GO


IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]

END
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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



ALTER TABLE Organization ALTER COLUMN [Plan] NVARCHAR(50) NULL;
ALTER TABLE Organization ALTER COLUMN PlanType tinyint NULL;
ALTER TABLE Organization ALTER COLUMN UseTotp bit NULL;
ALTER TABLE Organization ALTER COLUMN  UsersGetPremium bit NULL;


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
        PlanType = @PlanType,
        Seats = @Seats,
        MaxCollections = @MaxCollections,
        UseTotp = @UseTotp,
        UsersGetPremium = @UsersGetPremium,
        Storage = @Storage,
        MaxStorageGb = @MaxStorageGb,
        MaxAutoscaleSeats = @MaxAutoscaleSeats,
        RevisionDate = GETUTCDATE()
    WHERE
         [OrganizationId] = @OrganizationId
END
GO


IF OBJECT_ID('[dbo].[OrganizationSecretsManager_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSecretsManager_Update]
END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
        OrganizationId = @OrganizationId,
        [Plan] = @Plan,
        PlanType = @PlanType,
        UserSeats = @UserSeats,
        ServiceAccountSeats = @ServiceAccountSeats,
        UseEnvironments = @UseEnvironments,
        MaxAutoscaleUserSeats = @MaxAutoscaleUserSeats,
        MaxAutoscaleServiceAccounts = @MaxAutoscaleServiceAccounts,
        MaxProjects = @MaxProjects,
        RevisionDate = GETUTCDATE()
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Organization_ReadAbilities]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadAbilities]
END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
        ISNULL(O.[UsersGetPremium], OPM.UsersGetPremium) AS UsersGetPremium,
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

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    O.Id,
    O.Identifier,
    O.Name,
    O.BusinessName,
    O.BusinessAddress1,
    O.BusinessAddress2,
    O.BusinessAddress3,
    O.BusinessCountry,
    O.BusinessTaxNumber,
    O.BillingEmail,
    O.UsePolicies,
    O.UseSso,
    O.UseGroups,
    O.UseDirectory,
    O.UseEvents,
    O.Use2fA,
    O.UseApi,
    O.UseResetPassword,
    O.SelfHost,
    O.Gateway,
    O.GatewayCustomerId,
    O.GatewaySubscriptionId,
    O.ReferenceData,
    O.Enabled,
    O.LicenseKey,
    O.ApiKey,
    O.PublicKey,
    O.PrivateKey,
    O.TwoFactorProviders,
    O.ExpirationDate,
    O.CreationDate,
    O.RevisionDate,
    O.OwnersNotifiedOfAutoscaling,
    O.UseKeyConnector,
    ISNULL(O.MaxAutoscaleSeats, OPM.MaxAutoScaleSeats) As MaxAutoScaleSeats,
    ISNULL(O.UsersGetPremium, OPM.UsersGetPremium) As UsersGetPremium,
    ISNULL(O.Storage, OPM.Storage) As Storage,
    ISNULL(O.MaxStorageGb, OPM.MaxStorageGb) As MaxStorageGb,
    ISNULL(O.UseTotp, OPM.UseTotp) As UseTotp,
    ISNULL(OPM.[Plan], O.[Plan]) As [Plan],
    ISNULL(O.PlanType, OPM.PlanType) As PlanType,
    ISNULL(O.Seats, OPM.Seats) As Seats,
    ISNULL(O.MaxCollections, OPM.MaxCollections) As MaxCollections
FROM
    [dbo].[Organization] O
    LEFT JOIN OrganizationPasswordManager OPM on OPM.OrganizationId = O.Id
GO
