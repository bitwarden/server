IF COL_LENGTH('[dbo].[User]', 'RenewalReminderDate') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[User]
    ADD
        [RenewalReminderDate] DATETIME2 (7) NULL
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_User_Premium_PremiumExpirationDate_RenewalReminderDate'
    AND object_id = OBJECT_ID('[dbo].[User]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_Premium_PremiumExpirationDate_RenewalReminderDate]
        ON [dbo].[User]([Premium] ASC, [PremiumExpirationDate] ASC, [RenewalReminderDate] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Grant_ExpirationDate'
    AND object_id = OBJECT_ID('[dbo].[Grant]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Grant_ExpirationDate]
        ON [dbo].[Grant]([ExpirationDate] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_U2f_CreationDate'
    AND object_id = OBJECT_ID('[dbo].[U2f]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_U2f_CreationDate]
        ON [dbo].[U2f]([CreationDate] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_U2f_UserId'
    AND object_id = OBJECT_ID('[dbo].[U2f]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_U2f_UserId]
        ON [dbo].[U2f]([UserId] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Device_Identifier'
    AND object_id = OBJECT_ID('[dbo].[Device]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Device_Identifier]
        ON [dbo].[Device]([Identifier] ASC)
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

IF OBJECT_ID('[dbo].[U2f_DeleteOld]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_DeleteOld]
END
GO

CREATE PROCEDURE [dbo].[U2f_DeleteOld]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
    DECLARE @Threshold DATETIME2(7) = DATEADD (day, -7, GETUTCDATE())

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[U2f]
        WHERE
            [CreationDate] IS NULL
            OR [CreationDate] < @Threshold

        SET @BatchSize = @@ROWCOUNT
    END
END
GO

IF OBJECT_ID('[dbo].[Grant_DeleteExpired]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_DeleteExpired]
END
GO

CREATE PROCEDURE [dbo].[Grant_DeleteExpired]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Grant]
        WHERE
            [ExpirationDate] < @Now

        SET @BatchSize = @@ROWCOUNT
    END
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

IF OBJECT_ID('[dbo].[Folder_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Folder_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Folder_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM [dbo].[Folder] WHERE [Id] = @Id)
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$."', @UserId, '"')

    ;WITH [CTE] AS (
        SELECT
            [Id],
            [OrganizationId],
            [AccessAll]
        FROM
            [OrganizationUser]
        WHERE
            [UserId] = @UserId
            AND [Status] = 2 -- Confirmed
    )
    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    INNER JOIN
        [CTE] OU ON C.[UserId] IS NULL AND C.[OrganizationId] IN (SELECT [OrganizationId] FROM [CTE])
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId] AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
        AND JSON_VALUE(C.[Folders], @UserIdPath) = @Id

    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    WHERE
        [UserId] = @UserId
        AND JSON_VALUE([Folders], @UserIdPath) = @Id

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

IF OBJECT_ID('[dbo].[CollectionUserDetails_ReadByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUserDetails_ReadByCollectionId]
END
GO

CREATE PROCEDURE [dbo].[CollectionUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS [OrganizationUserId],
        CASE
            WHEN OU.[AccessAll] = 1 OR G.[AccessAll] = 1 THEN 1
            ELSE 0
        END [AccessAll],
        U.[Name],
        ISNULL(U.[Email], OU.[Email]) Email,
        OU.[Status],
        OU.[Type],
        CASE
            WHEN OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 0
            ELSE 1
        END [ReadOnly]
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        CU.[CollectionId] IS NOT NULL
        OR CG.[CollectionId] IS NOT NULL
        OR (
            OU.[OrganizationId] = @OrganizationId
            AND (
                OU.[AccessAll] = 1
                OR G.[AccessAll] = 1
            )
        )
END
GO
