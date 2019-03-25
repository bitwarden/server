IF OBJECT_ID('[dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadCountByOnlyOwner]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadCountByOnlyOwner]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOnlyOwner]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [OwnerCountCTE] AS
    (
        SELECT
            OU.[UserId],
            COUNT(1) OVER (PARTITION BY OU.[OrganizationId]) [ConfirmedOwnerCount]
        FROM
            [dbo].[OrganizationUser] OU
        WHERE
            OU.[Type] = 0 -- 0 = Owner
            AND OU.[Status] = 2 -- 2 = Confirmed
    )
    SELECT
        COUNT(1)
    FROM
        [OwnerCountCTE] OC
    WHERE
        OC.[UserId] = @UserId
        AND OC.[ConfirmedOwnerCount] = 1
END
GO

IF OBJECT_ID('[dbo].[User_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO

IF COL_LENGTH('[dbo].[Organization]', 'BusinessAddress1') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD 
        [BusinessAddress1]  NVARCHAR(50) NULL,
        [BusinessAddress2]  NVARCHAR(50) NULL,
        [BusinessAddress3]  NVARCHAR(50) NULL,
        [BusinessCountry]   VARCHAR(2)   NULL,
        [BusinessTaxNumber] NVARCHAR(30) NULL
END
GO

IF OBJECT_ID('[dbo].[Organization_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Create]
END
GO

CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseTotp BIT,
    @SelfHost BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
        [Id],
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
        [UseGroups],
        [UseDirectory],
        [UseTotp],
        [SelfHost],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [Enabled],
        [LicenseKey],
        [ExpirationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
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
        @UseGroups,
        @UseDirectory,
        @UseTotp,
        @SelfHost,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @Enabled,
        @LicenseKey,
        @ExpirationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

IF OBJECT_ID('[dbo].[Organization_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Update]
END
GO

CREATE PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseTotp BIT,
    @SelfHost BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)

AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Organization]
    SET
        [Name] = @Name,
        [BusinessName] = @BusinessName,
        [BusinessAddress1] = @BusinessAddress1,
        [BusinessAddress2] = @BusinessAddress2,
        [BusinessAddress3] = @BusinessAddress3,
        [BusinessCountry] = @BusinessCountry,
        [BusinessTaxNumber] = @BusinessTaxNumber,
        [BillingEmail] = @BillingEmail,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UseGroups] = @UseGroups,
        [UseDirectory] = @UseDirectory,
        [UseTotp] = @UseTotp,
        [SelfHost] = @SelfHost,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [Enabled] = @Enabled,
        [LicenseKey] = @LicenseKey,
        [ExpirationDate] = @ExpirationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteByUserId]
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteByUserId]
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByUserId_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @UserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByUserId_Ciphers
    END

    -- Cleanup user
    EXEC [dbo].[User_UpdateStorage] @UserId
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Attachments BIT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = CASE WHEN [Attachments] IS NOT NULL THEN 1 ELSE 0 END
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
