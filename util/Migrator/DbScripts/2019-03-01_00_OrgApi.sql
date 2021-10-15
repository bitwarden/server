-- Setup for random string generation

CREATE VIEW [dbo].[SecureRandomBytes]
AS
SELECT [RandBytes] = CRYPT_GEN_RANDOM(2)
GO

CREATE FUNCTION [dbo].[SecureRandomString]()
RETURNS varchar(30)
AS
BEGIN
    declare @sLength tinyint
    declare @randomString varchar(30)
    declare @counter tinyint
    declare @nextChar char(1)
    declare @rnd as float
    declare @bytes binary(2)

    set @sLength = 30
    set @counter = 1
    set @randomString = ''

    while @counter <= @sLength
    begin
        select @bytes = [RandBytes] from [dbo].[SecureRandomBytes]
        select @rnd = cast(cast(cast(@bytes as int) as float) / 65535 as float)
        select @nextChar = char(48 + convert(int, (122-48+1) * @rnd))
        if ascii(@nextChar) not in (58,59,60,61,62,63,64,91,92,93,94,95,96)
        begin
            select @randomString = @randomString + @nextChar
            set @counter = @counter + 1
        end
     end
     return @randomString 
END
GO

-- End setup

IF COL_LENGTH('[dbo].[Organization]', 'UseApi') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [UseApi] BIT NULL
END
GO

UPDATE
    [dbo].[Organization]
SET
    [UseApi] = (CASE WHEN [PlanType] = 5 OR [PlanType] = 4 THEN 1 ELSE 0 END)
WHERE
    [UseApi] IS NULL
GO

ALTER TABLE
    [dbo].[Organization]
ALTER COLUMN
    [UseApi] BIT NOT NULL
GO

IF COL_LENGTH('[dbo].[Organization]', 'ApiKey') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [ApiKey] VARCHAR(30) NULL
END
GO

UPDATE
    [dbo].[Organization]
SET
    [ApiKey] = (SELECT [dbo].[SecureRandomString]())
WHERE
    [ApiKey] IS NULL
GO

ALTER TABLE
    [dbo].[Organization]
ALTER COLUMN
    [ApiKey] VARCHAR(30) NOT NULL
GO

-- Cleanup random string generation

DROP VIEW [dbo].[SecureRandomBytes]
GO
DROP FUNCTION [dbo].[SecureRandomString]
GO

-- End

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
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ApiKey VARCHAR(30),
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
        [UseEvents],
        [UseTotp],
        [Use2fa],
        [UseApi],
        [SelfHost],
        [UsersGetPremium],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [Enabled],
        [LicenseKey],
        [ApiKey],
        [TwoFactorProviders],
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
        @UseEvents,
        @UseTotp,
        @Use2fa,
        @UseApi,
        @SelfHost,
        @UsersGetPremium,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @Enabled,
        @LicenseKey,
        @ApiKey,
        @TwoFactorProviders,
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
    @UseEvents BIT,
    @UseTotp BIT,
    @Use2fa BIT,
    @UseApi BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ApiKey VARCHAR(30),
    @TwoFactorProviders NVARCHAR(MAX),
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
        [UseEvents] = @UseEvents,
        [UseTotp] = @UseTotp,
        [Use2fa] = @Use2fa,
        [UseApi] = @UseApi,
        [SelfHost] = @SelfHost,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [Enabled] = @Enabled,
        [LicenseKey] = @LicenseKey,
        [ApiKey] = @ApiKey,
        [TwoFactorProviders] = @TwoFactorProviders,
        [ExpirationDate] = @ExpirationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationView')
BEGIN
    DROP VIEW [dbo].[OrganizationView]
END
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserOrganizationDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserOrganizationDetailsView]
END
GO

CREATE VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    O.[Name],
    O.[Enabled],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[Seats],
    O.[MaxCollections],
    O.[MaxStorageGb],
    OU.[Key],
    OU.[Status],
    OU.[Type]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUserUserDetails_ReadById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUserUserDetails_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = [OU].[Id]
    WHERE
        [OrganizationUserId] = @Id
END
GO

IF COL_LENGTH('[dbo].[Collection]', 'ExternalId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Collection]
    ADD
        [ExternalId] NVARCHAR(300) NULL
END
GO

IF OBJECT_ID('[dbo].[Collection_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_Create]
END
GO

CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER,
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

IF OBJECT_ID('[dbo].[Collection_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_Update]
END
GO

CREATE PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Collection_CreateWithGroups]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_CreateWithGroups]
END
GO

CREATE PROCEDURE [dbo].[Collection_CreateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        @Id,
        [Id],
        [ReadOnly]
    FROM
        @Groups
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Collection_UpdateWithGroups]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_UpdateWithGroups]
END
GO

CREATE PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Group]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @Groups AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[GroupId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            @Id,
            [Source].[Id],
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CollectionId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'CollectionView')
BEGIN
    DROP VIEW [dbo].[CollectionView]
END
GO

CREATE VIEW [dbo].[CollectionView]
AS
SELECT
    *
FROM
    [dbo].[Collection]
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCollectionDetails]
END
GO

CREATE FUNCTION [dbo].[UserCollectionDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
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
GO
