/****************************************************************
 *
 * WARNING: Index Rebuild on OrganizationUser Table!
 * Ensure [IX_OrganizationUser_UserIdOrganizationIdStatus] impact is done after-hours
 *      or scale DB instance up to handle increased load during update.
 *
 ***************************************************************/

PRINT N'Starting migration for 2022-06-08_00_DeactivatedUserStatus';
GO

PRINT N'Checking dbo.OrganizationUser.Status is TINYINT...';
GO
IF EXISTS (
    SELECT  TOP 1 NULL
    FROM    [information_schema].[columns]
    WHERE   [table_name] = 'OrganizationUser'
            AND [table_schema] = 'dbo'
            AND [column_name] = 'Status'
            AND [data_type] = 'TINYINT'
)
BEGIN
    PRINT N'Dropping index IX_OrganizationUser_UserIdOrganizationIdStatus...';
    DROP INDEX IF EXISTS [IX_OrganizationUser_UserIdOrganizationIdStatus]
        ON [dbo].[OrganizationUser];

    PRINT N'Altering dbo.OrganizationUser.Status to SMALLINT...';
    ALTER TABLE [dbo].[OrganizationUser]
        ALTER COLUMN [Status] SMALLINT NOT NULL;

    PRINT N'Recreating index IX_OrganizationUser_UserIdOrganizationIdStatus...';
    CREATE NONCLUSTERED INDEX [IX_OrganizationUser_UserIdOrganizationIdStatus]
        ON [dbo].[OrganizationUser]([UserId] ASC, [OrganizationId] ASC, [Status] ASC)
        INCLUDE ([AccessAll]);
END
GO

PRINT N'Dropping stored procedure, dbo.OrganizationUser_CreateMany...';
GO
IF OBJECT_ID('[dbo].[OrganizationUser_CreateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateMany]
END
GO

PRINT N'Dropping stored procedure, dbo.OrganizationUser_UpdateMany...';
GO
IF OBJECT_ID('[dbo].[OrganizationUser_UpdateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateMany]
END
GO

PRINT N'Dropping type dbo.OrganizationUserType...';
GO
IF TYPE_ID(N'[dbo].[OrganizationUserType]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[OrganizationUserType];
END
GO
PRINT N'Recreating Type dbo.OrganizationUserType...';
GO
CREATE TYPE [dbo].[OrganizationUserType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [OrganizationId] UNIQUEIDENTIFIER,
    [UserId] UNIQUEIDENTIFIER,
    [Email] NVARCHAR(256),
    [Key] VARCHAR(MAX),
    [Status] SMALLINT,
    [Type] TINYINT,
    [AccessAll] BIT,
    [ExternalId] NVARCHAR(300),
    [CreationDate] DATETIME2(7),
    [RevisionDate] DATETIME2(7),
    [Permissions] NVARCHAR(MAX),
    [ResetPasswordKey] VARCHAR(MAX)
);
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_CreateMany...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateMany]
    @OrganizationUsersInput [dbo].[OrganizationUserType] READONLY
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
    SELECT
        OU.[Id],
        OU.[OrganizationId],
        OU.[UserId],
        OU.[Email],
        OU.[Key],
        OU.[Status],
        OU.[Type],
        OU.[AccessAll],
        OU.[ExternalId],
        OU.[CreationDate],
        OU.[RevisionDate],
        OU.[Permissions],
        OU.[ResetPasswordKey]
    FROM
        @OrganizationUsersInput OU
END
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_UpdateMany...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateMany]
    @OrganizationUsersInput [dbo].[OrganizationUserType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        [OrganizationId] = OUI.[OrganizationId],
        [UserId] = OUI.[UserId],
        [Email] = OUI.[Email],
        [Key] = OUI.[Key],
        [Status] = OUI.[Status],
        [Type] = OUI.[Type],
        [AccessAll] = OUI.[AccessAll],
        [ExternalId] = OUI.[ExternalId],
        [CreationDate] = OUI.[CreationDate],
        [RevisionDate] = OUI.[RevisionDate],
        [Permissions] = OUI.[Permissions],
        [ResetPasswordKey] = OUI.[ResetPasswordKey]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUsersInput OUI ON OU.Id = OUI.Id

    EXEC [dbo].[User_BumpManyAccountRevisionDates]
    (
        SELECT UserId
        FROM @OrganizationUsersInput
    )
END
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_Create...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
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

PRINT N'Altering stored procedure, dbo.OrganizationUser_CreateWithCollections...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey

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

PRINT N'Altering stored procedure, dbo.OrganizationUser_Update...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
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
        [Permissions] = @Permissions,
        [ResetPasswordKey] = @ResetPasswordKey
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_UpdateWithCollections...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey
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

PRINT N'Altering stored procedure, dbo.OrganizationUserOrganizationDetails_ReadByUserIdStatus...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND (@Status IS NULL OR [Status] = @Status)
END
GO

PRINT N'Altering stored procedure, dbo.OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId...';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND [OrganizationId] = @OrganizationId
        AND (@Status IS NULL OR [Status] = @Status)
END
GO

PRINT N'Altering function, dbo.PolicyApplicableToUser...';
GO
CREATE OR ALTER FUNCTION [dbo].[PolicyApplicableToUser]
(
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
)
RETURNS TABLE
AS RETURN
SELECT
    P.*
FROM
    [dbo].[PolicyView] P
INNER JOIN
    [dbo].[OrganizationUserView] OU ON P.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    (SELECT
        PU.UserId,
        PO.OrganizationId
    FROM
        [dbo].[ProviderUserView] PU
    INNER JOIN
        [ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]) PUPO
    ON PUPO.UserId = OU.UserId
    AND PUPO.OrganizationId = P.OrganizationId
WHERE
    (
        (
            OU.[Status] > 0
            AND OU.[UserId] = @UserId 
        )
        OR (
            OU.[Status] = 0 -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
            AND OU.[Email] IN (SELECT U.Email FROM [dbo].[UserView] U WHERE U.Id = @UserId)
        )
    )
    AND P.[Type] = @PolicyType
    AND P.[Enabled] = 1
    AND OU.[Status] >= @MinimumStatus
    AND OU.[Type] >= 2              -- Not an owner (0) or admin (1)
    AND (                           -- Can't manage policies
        OU.[Permissions] IS NULL
        OR COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false'
    )
    AND PUPO.[UserId] IS NULL   -- Not a provider
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_Deactivate';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Deactivate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = -1 -- Deactivated
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
GO

PRINT N'Altering stored procedure, dbo.OrganizationUser_Activate';
GO
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Activate]
    @Id UNIQUEIDENTIFIER,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = @Status
    WHERE
        [Id] = @Id
        AND [Status] = -1 -- Deactivated

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
GO

PRINT N'Finished migration for 2022-06-08_00_DeactivatedUserStatus';
GO