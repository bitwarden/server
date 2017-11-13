IF OBJECT_ID('[dbo].[OrganizationUser_ReadByOrganizationIdEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdEmail]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadCountByOrganizationIdEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationIdEmail]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[User] U ON OU.[UserId] = U.[Id]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND (
            (@OnlyUsers = 0 AND (OU.[Email] = @Email OR U.[Email] = @Email))
            OR (@OnlyUsers = 1 AND U.[Email] = @Email)
        )
END
GO

IF EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name]='IX_Cipher_UserId_Type'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    DROP INDEX [IX_Cipher_UserId_Type] ON [dbo].[Cipher]
END
GO

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name]='IX_Cipher_UserId_Type_IncludeAll'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_Type_IncludeAll]
        ON [dbo].[Cipher]([UserId] ASC, [Type] ASC)
        INCLUDE ([OrganizationId], [Data], [Favorites], [Folders], [Attachments], [CreationDate], [RevisionDate])
END
GO

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_Folder_UserId_IncludeAll'
    AND object_id = OBJECT_ID('[dbo].[Folder]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Folder_UserId_IncludeAll]
        ON [dbo].[Folder]([UserId] ASC)
        INCLUDE ([Name], [CreationDate], [RevisionDate])
END
GO

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_OrganizationUser_UserIdOrganizationIdStatus'
    AND object_id = OBJECT_ID('[dbo].[OrganizationUser]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationUser_UserIdOrganizationIdStatus]
        ON [dbo].[OrganizationUser]([UserId] ASC,[OrganizationId] ASC,[Status] ASC)
        INCLUDE ([AccessAll])
END
GO
