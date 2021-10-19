-- Tech debt: creates a few views and procedures that we implement as base repository methods but that would throw exceptions on use for not existing in the DB
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'SsoUserView')
BEGIN
    DROP VIEW [dbo].[SsoUserView];
END
GO

CREATE VIEW [dbo].[SsoUserView]
AS
SELECT
    *
FROM
    [dbo].[SsoUser]
GO

IF OBJECT_ID('[dbo].[SsoConfig_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_ReadById]
END
GO

CREATE PROCEDURE [dbo].[SsoConfig_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[SsoUser_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_DeleteById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[SsoConfig_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_DeleteById]
END
GO
CREATE PROCEDURE [dbo].[SsoConfig_DeleteById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Event_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Event_ReadById]
END
GO
CREATE PROCEDURE [dbo].[Event_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Event]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[U2f_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_ReadById]
END
GO
CREATE PROCEDURE [dbo].[U2f_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[U2f]
    WHERE
        [Id] = @Id
END
GO
-- Refactor: Set all the base Create procs to output the ID for testing
IF OBJECT_ID('[dbo].[Organization_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Create]
END
GO

CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats INT,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
    @UseResetPassword BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ApiKey VARCHAR(30),
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
        [Id],
        [Identifier],
        [Name],
        [BusinessName],
        [BusinessAddress1],
        [BusinessAddress2],
        [BusinessAddress3],
        [BusinessCountry],
        [BusinessTaxNumber],
        [BillingEmail],
        [Plan],
        [PlanType],
        [Seats],
        [MaxCollections],
        [UsePolicies],
        [UseSso],
        [UseGroups],
        [UseDirectory],
        [UseEvents],
        [UseTotp],
        [Use2fa],
        [UseApi],
        [UseResetPassword],
        [SelfHost],
        [UsersGetPremium],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [ReferenceData],
        [Enabled],
        [LicenseKey],
        [ApiKey],
        [PublicKey],
        [PrivateKey],
        [TwoFactorProviders],
        [ExpirationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Identifier,
        @Name,
        @BusinessName,
        @BusinessAddress1,
        @BusinessAddress2,
        @BusinessAddress3,
        @BusinessCountry,
        @BusinessTaxNumber,
        @BillingEmail,
        @Plan,
        @PlanType,
        @Seats,
        @MaxCollections,
        @UsePolicies,
        @UseSso,
        @UseGroups,
        @UseDirectory,
        @UseEvents,
        @UseTotp,
        @Use2fa,
        @UseApi,
        @UseResetPassword,
        @SelfHost,
        @UsersGetPremium,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ReferenceData,
        @Enabled,
        @LicenseKey,
        @ApiKey,
        @PublicKey,
        @PrivateKey,
        @TwoFactorProviders,
        @ExpirationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO 

IF OBJECT_ID('[dbo].[User_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Create]
END
GO

CREATE PROCEDURE [dbo].[User_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(50),
    @Email NVARCHAR(256),
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
    @ReferenceData VARCHAR(MAX),
    @LicenseKey VARCHAR(100),
    @Kdf TINYINT,
    @KdfIterations INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ApiKey VARCHAR(30)
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
        [ReferenceData],
        [LicenseKey],
        [Kdf],
        [KdfIterations],
        [CreationDate],
        [RevisionDate],
        [ApiKey]
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
        @ReferenceData,
        @LicenseKey,
        @Kdf,
        @KdfIterations,
        @CreationDate,
        @RevisionDate,
        @ApiKey
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationUser]
    (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate,
        @Permissions,
        @ResetPasswordKey
    )
END
GO

IF OBJECT_ID('[dbo].[Cipher_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Create]
END
GO

CREATE PROCEDURE [dbo].[Cipher_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [DeletedDate],
        [Reprompt]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @Attachments,
        @CreationDate,
        @RevisionDate,
        @DeletedDate,
        @Reprompt
    )

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Device_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Device_Create]
END
GO
CREATE PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[Collection_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_Create]
END
GO
CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[EmergencyAccess_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[EmergencyAccess_Create]
END
GO
CREATE PROCEDURE [dbo].[EmergencyAccess_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @GrantorId UNIQUEIDENTIFIER,
    @GranteeId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @KeyEncrypted VARCHAR(MAX),
    @Type TINYINT,
    @Status TINYINT,
    @WaitTimeDays SMALLINT,
    @RecoveryInitiatedDate DATETIME2(7),
    @LastNotificationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[EmergencyAccess]
    (
        [Id],
        [GrantorId],
        [GranteeId],
        [Email],
        [KeyEncrypted],
        [Type],
        [Status],
        [WaitTimeDays],
        [RecoveryInitiatedDate],
        [LastNotificationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @GrantorId,
        @GranteeId,
        @Email,
        @KeyEncrypted,
        @Type,
        @Status,
        @WaitTimeDays,
        @RecoveryInitiatedDate,
        @LastNotificationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[Event_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Event_Create]
END
GO
CREATE PROCEDURE [dbo].[Event_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Type INT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @PolicyId UNIQUEIDENTIFIER,
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ActingUserId UNIQUEIDENTIFIER,
    @DeviceType SMALLINT,
    @IpAddress VARCHAR(50),
    @Date DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Event]
    (
        [Id],
        [Type],
        [UserId],
        [OrganizationId],
        [CipherId],
        [CollectionId],
        [PolicyId],
        [GroupId],
        [OrganizationUserId],
        [ActingUserId],
        [DeviceType],
        [IpAddress],
        [Date]
    )
    VALUES
    (
        @Id,
        @Type,
        @UserId,
        @OrganizationId,
        @CipherId,
        @CollectionId,
        @PolicyId,
        @GroupId,
        @OrganizationUserId,
        @ActingUserId,
        @DeviceType,
        @IpAddress,
        @Date
    )
END
GO

IF OBJECT_ID('[dbo].[Folder_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Folder_Create]
END
GO

CREATE PROCEDURE [dbo].[Folder_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Folder]
    (
        [Id],
        [UserId],
        [Name],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
IF OBJECT_ID('[dbo].[Group_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_Create]
END
GO
CREATE PROCEDURE [dbo].[Group_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(100),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Group]
    (
        [Id],
        [OrganizationId],
        [Name],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )
END
GO
IF OBJECT_ID('[dbo].[Installation_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Installation_Create]
END
GO
CREATE PROCEDURE [dbo].[Installation_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Installation]
    (
        [Id],
        [Email],
        [Key],
        [Enabled],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @Email,
        @Key,
        @Enabled,
        @CreationDate
    )
END
GO
IF OBJECT_ID('[dbo].[Policy_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_Create]
END
GO
CREATE PROCEDURE [dbo].[Policy_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Policy]
    (
        [Id],
        [OrganizationId],
        [Type],
        [Data],
        [Enabled],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Type,
        @Data,
        @Enabled,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Send_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_Create]
END
GO

CREATE PROCEDURE [dbo].[Send_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @Password NVARCHAR(300),
    @MaxAccessCount INT,
    @AccessCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7),
    @DeletionDate DATETIME2(7),
    @Disabled BIT,
    @HideEmail BIT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Send]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Key],
        [Password],
        [MaxAccessCount],
        [AccessCount],
        [CreationDate],
        [RevisionDate],
        [ExpirationDate],
        [DeletionDate],
        [Disabled],
        [HideEmail]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Key,
        @Password,
        @MaxAccessCount,
        @AccessCount,
        @CreationDate,
        @RevisionDate,
        @ExpirationDate,
        @DeletionDate,
        @Disabled,
        @HideEmail
    )

    IF @UserId IS NOT NULL
    BEGIN
        IF @Type = 1 --File
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
GO

IF OBJECT_ID('[dbo].[TaxRate_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_Create]
END
GO


CREATE PROCEDURE [dbo].[TaxRate_Create]
    @Id VARCHAR(40) OUTPUT,
    @Country VARCHAR(50),
    @State VARCHAR(2),
    @PostalCode VARCHAR(10),
    @Rate DECIMAL(5,2),
    @Active BIT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[TaxRate]
    (
        [Id],
        [Country],
        [State],
        [PostalCode],
        [Rate],
        [Active]
    )
    VALUES
    (
        @Id,
        @Country,
        @State,
        @PostalCode,
        @Rate,
        1
    )
END
GO

IF OBJECT_ID('[dbo].[Transaction_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Transaction_Create]
END
GO

CREATE PROCEDURE [dbo].[Transaction_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Amount MONEY,
    @Refunded BIT,
    @RefundedAmount MONEY,
    @Details NVARCHAR(100),
    @PaymentMethodType TINYINT,
    @Gateway TINYINT,
    @GatewayId VARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Transaction]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Amount],
        [Refunded],
        [RefundedAmount],
        [Details],
        [PaymentMethodType],
        [Gateway],
        [GatewayId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Amount,
        @Refunded,
        @RefundedAmount,
        @Details,
        @PaymentMethodType,
        @Gateway,
        @GatewayId,
        @CreationDate
    )
END
GO

IF OBJECT_ID('[dbo].[U2f_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_Create]
END
GO

CREATE PROCEDURE [dbo].[U2f_Create]
    @Id INT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @KeyHandle VARCHAR(200),
    @Challenge VARCHAR(200),
    @AppId VARCHAR(50),
    @Version VARCHAR(20),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[U2f]
    (
        [UserId],
        [KeyHandle],
        [Challenge],
        [AppId],
        [Version],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @KeyHandle,
        @Challenge,
        @AppId,
        @Version,
        @CreationDate
    )

    SET @Id = (SELECT scope_identity())
END
GO
