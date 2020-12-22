IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessBusinessPortal') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [AccessBusinessPortal] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessEventLogs') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [AccessEventLogs] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessImportExport') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [AccessImportExport] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessReports') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [AccessReports] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'ManageAllCollections') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [ManageAllCollections] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'ManageAssignedCollections') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [ManageAssignedCollections] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'ManageGroups') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [ManageGroups] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'ManagePolicies') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [ManagePolicies] BIT NULL
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'ManageUsers') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [ManageUsers] BIT NULL
END
GO

UPDATE [dbo].[OrganizationUser] 
SET 
    AccessBusinessPortal = 0,
    AccessEventLogs = 0,
    AccessImportExport = 0,
    AccessReports = 0,
    ManageAllCollections = 0,
    ManageAssignedCollections = 0,
    ManageGroups = 0,
    ManageUsers = 0
WHERE 
    AccessBusinessPortal IS NULL
    OR AccessEventLogs IS NULL
    OR AccessImportExport IS NULL
    OR AccessReports IS NULL
    OR ManageAllCollections IS NULL
    OR ManageAssignedCollections IS NULL
    OR ManageGroups IS NULL
    OR ManageUsers IS NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    AccessBusinessPortal BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    AccessEventLogs BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    AccessImportExport BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    AccessReports BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    ManageAllCollections BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    ManageAssignedCollections BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    ManageGroups BIT NOT NULL
GO

ALTER TABLE 
    [dbo].[OrganizationUser]
ALTER COLUMN
    ManageUsers BIT NOT NULL
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserView];
END
GO

CREATE VIEW [dbo].[OrganizationUserView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationUser]

GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserOrganizationDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserOrganizationDetailsView];
END
GO

CREATE VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    O.[Name],
    O.[Enabled],
    O.[UsePolicies],
    O.[UseSso],
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
    O.[Identifier],
    OU.[Key],
    OU.[Status],
    OU.[Type],
    SU.[ExternalId] SsoExternalId,
    OU.[AccessBusinessPortal],
    OU.[AccessEventLogs],
    OU.[AccessImportExport],
    OU.[AccessReports],
    OU.[ManageAllCollections],
    OU.[ManageAssignedCollections],
    OU.[ManageGroups],
    OU.[ManagePolicies],
    OU.[ManageUsers]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationUserUserDetailsView')
BEGIN
    DROP VIEW [dbo].[OrganizationUserUserDetailsView];
END
GO

CREATE VIEW [dbo].[OrganizationUserUserDetailsView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    U.[TwoFactorProviders],
    U.[Premium],
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[ExternalId],
    SU.[ExternalId] SsoExternalId,
    OU.[AccessBusinessPortal],
    OU.[AccessEventLogs],
    OU.[AccessImportExport],
    OU.[AccessReports],
    OU.[ManageAllCollections],
    OU.[ManageAssignedCollections],
    OU.[ManageGroups],
    OU.[ManagePolicies],
    OU.[ManageUsers]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
GO

IF OBJECT_ID('[dbo].[OrganizationUser_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @AccessBusinessPortal BIT,
    @AccessEventLogs BIT,
    @AccessImportExport BIT,
    @AccessReports BIT,
    @ManageAllCollections BIT,
    @ManageAssignedCollections BIT,
    @ManageGroups BIT,
    @ManagePolicies BIT,
    @ManageUsers BIT
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
        [AccessBusinessPortal],
        [AccessEventLogs],
        [AccessImportExport],
        [AccessReports],
        [ManageAllCollections],
        [ManageAssignedCollections],
        [ManageGroups],
        [ManagePolicies],
        [ManageUsers]
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
        @AccessBusinessPortal,
        @AccessEventLogs,
        @AccessImportExport,
        @AccessReports,
        @ManageAllCollections,
        @ManageAssignedCollections,
        @ManageGroups,
        @ManagePolicies,
        @ManageUsers
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @AccessBusinessPortal BIT,
    @AccessEventLogs BIT,
    @AccessImportExport BIT,
    @AccessReports BIT,
    @ManageAllCollections BIT,
    @ManageAssignedCollections BIT,
    @ManageGroups BIT,
    @ManagePolicies BIT,
    @ManageUsers BIT,
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate, @AccessBusinessPortal, @AccessEventLogs, @AccessImportExport, @AccessReports, @ManageAllCollections, @ManageAssignedCollections,  @ManageGroups, @ManagePolicies, @ManageUsers

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords]
    )
    SELECT
        [Id],
        @Id,
        [ReadOnly],
        [HidePasswords]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @AccessBusinessPortal BIT,
    @AccessEventLogs BIT,
    @AccessImportExport BIT,
    @AccessReports BIT,
    @ManageAllCollections BIT,
    @ManageAssignedCollections BIT,
    @ManageGroups BIT,
    @ManagePolicies BIT,
    @ManageUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [OrganizationId] = @OrganizationId,
        [UserId] = @UserId,
        [Email] = @Email,
        [Key] = @Key,
        [Status] = @Status,
        [Type] = @Type,
        [AccessAll] = @AccessAll,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [AccessBusinessPortal] = @AccessBusinessPortal,
        [AccessEventLogs] = @AccessEventLogs,
        [AccessImportExport] = @AccessImportExport,
        [AccessReports] = @AccessReports,
        [ManageAllCollections] = @ManageAllCollections,
        [ManageAssignedCollections] = @ManageAssignedCollections,
        [ManageGroups] = @ManageGroups,
        [ManagePolicies] = @ManagePolicies,
        [ManageUsers] = @ManageUsers
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @AccessBusinessPortal BIT,
    @AccessEventLogs BIT,
    @AccessImportExport BIT,
    @AccessReports BIT,
    @ManageAllCollections BIT,
    @ManageAssignedCollections BIT,
    @ManageGroups BIT,
    @ManagePolicies BIT,
    @ManageUsers BIT,
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate, @AccessBusinessPortal, @AccessEventLogs, @AccessImportExport, @AccessReports, @ManageAllCollections, @ManageAssignedCollections, @ManageGroups, @ManagePolicies, @ManageUsers


    -- Update
    UPDATE
        [Target]
    SET
        [Target].[ReadOnly] = [Source].[ReadOnly],
        [Target].[HidePasswords] = [Source].[HidePasswords]
    FROM
        [dbo].[CollectionUser] AS [Target]
    INNER JOIN
        @Collections AS [Source] ON [Source].[Id] = [Target].[CollectionId]
    WHERE
        [Target].[OrganizationUserId] = @Id
        AND (
            [Target].[ReadOnly] != [Source].[ReadOnly]
            OR [Target].[HidePasswords] != [Source].[HidePasswords]
        )

    -- Insert
    INSERT INTO
        [dbo].[CollectionUser]
    SELECT
        [Source].[Id],
        @Id,
        [Source].[ReadOnly],
        [Source].[HidePasswords]
    FROM
        @Collections AS [Source]
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = [Source].[Id] AND C.[OrganizationId] = @OrganizationId
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[CollectionUser]
            WHERE
                [CollectionId] = [Source].[Id]
                AND [OrganizationUserId] = @Id
        )
    
    -- Delete
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    WHERE
        CU.[OrganizationUserId] = @Id
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @Collections
            WHERE
                [Id] = CU.[CollectionId]
        )
END
GO
