-- Create OrganizationUser Type
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE 
        [Name] = 'OrganizationUserType' AND
        is_user_defined = 1
)
BEGIN
CREATE TYPE [dbo].[OrganizationUserType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [OrganizationId] UNIQUEIDENTIFIER,
    [UserId] UNIQUEIDENTIFIER,
    [Email] NVARCHAR(256),
    [Key] VARCHAR(MAX),
    [Status] TINYINT,
    [Type] TINYINT,
    [AccessAll] BIT,
    [ExternalId] NVARCHAR(300),
    [CreationDate] DATETIME2(7),
    [RevisionDate] DATETIME2(7),
    [Permissions] NVARCHAR(MAX),
    [ResetPasswordKey] VARCHAR(MAX)
)
END
GO

-- Create many sproc
IF OBJECT_ID('[dbo].[OrganizationUser_CreateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateMany]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_CreateMany]
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

-- Bump many user account revision dates
IF OBJECT_ID('[dbo].[User_BumpManyAccountRevisionDates]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpManyAccountRevisionDates]
END
GO

CREATE PROCEDURE [dbo].[User_BumpManyAccountRevisionDates]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        [AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        @Ids IDs ON IDs.Id = U.Id
END
GO

-- Update many OrganizationUsers
IF OBJECT_ID('[dbo].[OrganizationUser_UpdateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateMany]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_UpdateMany]
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
