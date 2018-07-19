IF COL_LENGTH('[dbo].[User]', 'RenewalReminderDate') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[User]
    ADD
        [RenewalReminderDate] DATETIME2 (7) NULL
END
GO

IF OBJECT_ID('[dbo].[User_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Create]
END
GO

CREATE PROCEDURE [dbo].[User_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Email NVARCHAR(50),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @RenewalReminderDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @LicenseKey VARCHAR(100),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[User]
    (
        [Id],
        [Name],
        [Email],
        [EmailVerified],
        [MasterPassword],
        [MasterPasswordHint],
        [Culture],
        [SecurityStamp],
        [TwoFactorProviders],
        [TwoFactorRecoveryCode],
        [EquivalentDomains],
        [ExcludedGlobalEquivalentDomains],
        [AccountRevisionDate],
        [Key],
        [PublicKey],
        [PrivateKey],
        [Premium],
        [PremiumExpirationDate],
        [RenewalReminderDate],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [LicenseKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Name,
        @Email,
        @EmailVerified,
        @MasterPassword,
        @MasterPasswordHint,
        @Culture,
        @SecurityStamp,
        @TwoFactorProviders,
        @TwoFactorRecoveryCode,
        @EquivalentDomains,
        @ExcludedGlobalEquivalentDomains,
        @AccountRevisionDate,
        @Key,
        @PublicKey,
        @PrivateKey,
        @Premium,
        @PremiumExpirationDate,
        @RenewalReminderDate,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @LicenseKey,
        @CreationDate,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[User_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Update]
END
GO

CREATE PROCEDURE [dbo].[User_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Email NVARCHAR(50),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @RenewalReminderDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @LicenseKey VARCHAR(100),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Name] = @Name,
        [Email] = @Email,
        [EmailVerified] = @EmailVerified,
        [MasterPassword] = @MasterPassword,
        [MasterPasswordHint] = @MasterPasswordHint,
        [Culture] = @Culture,
        [SecurityStamp] = @SecurityStamp,
        [TwoFactorProviders] = @TwoFactorProviders,
        [TwoFactorRecoveryCode] = @TwoFactorRecoveryCode,
        [EquivalentDomains] = @EquivalentDomains,
        [ExcludedGlobalEquivalentDomains] = @ExcludedGlobalEquivalentDomains,
        [AccountRevisionDate] = @AccountRevisionDate,
        [Key] = @Key,
        [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey,
        [Premium] = @Premium,
        [PremiumExpirationDate] = @PremiumExpirationDate,
        [RenewalReminderDate] = @RenewalReminderDate,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [LicenseKey] = @LicenseKey,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[User_UpdateRenewalReminderDate]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_UpdateRenewalReminderDate]
END
GO

CREATE PROCEDURE [dbo].[User_UpdateRenewalReminderDate]
    @Id UNIQUEIDENTIFIER,
    @RenewalReminderDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [RenewalReminderDate] = @RenewalReminderDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[User_ReadByPremiumRenewal]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadByPremiumRenewal]
END
GO

CREATE PROCEDURE [dbo].[User_ReadByPremiumRenewal]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @WindowRef DATETIME2(7) = GETUTCDATE()
    DECLARE @WindowStart DATETIME2(7) = DATEADD (day, -15, @WindowRef)
    DECLARE @WindowEnd DATETIME2(7) = DATEADD (day, 15, @WindowRef)

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Premium] = 1
        AND [PremiumExpirationDate] >= @WindowRef
        AND [PremiumExpirationDate] < @WindowEnd
        AND (
            [RenewalReminderDate] IS NULL
            OR [RenewalReminderDate] < @WindowStart
        )
        AND [Gateway] = 1 -- Braintree
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE
            WHEN
                OU.[AccessAll] = 1
                OR G.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR CG.[ReadOnly] = 0
            THEN 0
            ELSE 1
        END [ReadOnly]
    FROM
        [dbo].[CollectionView] C
    INNER JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO

IF OBJECT_ID('[dbo].[Organization_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

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
        @Storage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #OrgStorageUpdateTemp

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[User_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    CREATE TABLE #UserStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #UserStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id

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
            #UserStorageUpdateTemp
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [CTE]

    DROP TABLE #UserStorageUpdateTemp

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollectionsForCiphers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsForCiphers]
END
GO

CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsForCiphers]
    @CipherIds AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #AvailableCollections (
        [Id] UNIQUEIDENTIFIER
    )

    INSERT INTO #AvailableCollections
        SELECT
            C.[Id]
        FROM
            [dbo].[Collection] C
        INNER JOIN
            [Organization] O ON O.[Id] = C.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId]
        WHERE
            O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR G.[AccessAll] = 1
                OR CG.[ReadOnly] = 0
            )

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No writable collections available to share with in this organization.
        RETURN
    END

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    SELECT 
        [Collection].[Id],
        [Cipher].[Id]
    FROM
        @CollectionIds [Collection]
    INNER JOIN
        @CipherIds [Cipher] ON 1 = 1
    WHERE
        [Collection].[Id] IN (SELECT [Id] FROM #AvailableCollections)

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'UserView')
BEGIN
    DROP VIEW [dbo].[UserView]
END
GO

CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
GO
