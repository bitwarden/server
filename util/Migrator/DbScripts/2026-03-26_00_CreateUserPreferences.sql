-- Create UserPreferences table
IF OBJECT_ID('dbo.UserPreferences') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserPreferences] (
        [Id]           UNIQUEIDENTIFIER NOT NULL,
        [UserId]       UNIQUEIDENTIFIER NOT NULL,
        [Data]         VARCHAR (MAX)    NOT NULL,
        [CreationDate] DATETIME2 (7)    NOT NULL,
        [RevisionDate] DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserPreferences_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_UserPreferences_UserId]
        ON [dbo].[UserPreferences]([UserId] ASC);
END
GO

-- Create UserPreferences_Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Data VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[UserPreferences]
    (
        [Id],
        [UserId],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Data,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Create UserPreferences_Update stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Data VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[UserPreferences]
    SET
        [UserId] = @UserId,
        [Data] = @Data,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

-- Create UserPreferences_ReadByUserId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
GO

-- Create UserPreferences_DeleteByUserId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
GO

-- Update User_DeleteById to clean up UserPreferences
CREATE OR ALTER PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers
        DELETE TOP(@BatchSize) FROM [dbo].[Cipher] WHERE [UserId] = @Id
        SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    DELETE FROM [dbo].[WebAuthnCredential] WHERE [UserId] = @Id
    DELETE FROM [dbo].[UserPreferences] WHERE [UserId] = @Id
    DELETE FROM [dbo].[Folder] WHERE [UserId] = @Id
    DELETE FROM [dbo].[AuthRequest] WHERE [UserId] = @Id
    DELETE FROM [dbo].[Device] WHERE [UserId] = @Id

    DECLARE @OrgUserIds [dbo].[GuidIdArray]
    INSERT INTO @OrgUserIds (Id) SELECT [Id] FROM [dbo].[OrganizationUser] WHERE [UserId] = @Id
    IF EXISTS (SELECT 1 FROM @OrgUserIds)
    BEGIN
        EXEC [dbo].[OrganizationUser_MigrateDefaultCollection] @OrgUserIds
    END

    DELETE CU FROM [dbo].[CollectionUser] CU INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId] WHERE OU.[UserId] = @Id
    DELETE GU FROM [dbo].[GroupUser] GU INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId] WHERE OU.[UserId] = @Id
    DELETE AP FROM [dbo].[AccessPolicy] AP INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId] WHERE [UserId] = @Id
    DELETE FROM [dbo].[OrganizationUser] WHERE [UserId] = @Id
    DELETE FROM [dbo].[ProviderUser] WHERE [UserId] = @Id
    DELETE FROM [dbo].[SsoUser] WHERE [UserId] = @Id
    DELETE FROM [dbo].[EmergencyAccess] WHERE [GrantorId] = @Id OR [GranteeId] = @Id
    DELETE FROM [dbo].[Send] WHERE [UserId] = @Id
    DELETE FROM [dbo].[NotificationStatus] WHERE [UserId] = @Id
    DELETE FROM [dbo].[Notification] WHERE [UserId] = @Id
    DELETE FROM [dbo].[User] WHERE [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO

-- Update User_DeleteByIds to clean up UserPreferences
CREATE OR ALTER PROCEDURE [dbo].[User_DeleteByIds]
    @Ids NVARCHAR(MAX)
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);
    INSERT INTO @ParsedIds (Id) SELECT value FROM OPENJSON(@Ids);

    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers
        DELETE TOP(@BatchSize) FROM [dbo].[Cipher] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
        SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    DELETE FROM [dbo].[WebAuthnCredential] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[UserPreferences] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[Folder] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[AuthRequest] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[Device] WHERE [UserId] IN (SELECT * FROM @ParsedIds)

    DECLARE @OrgUserIds [dbo].[GuidIdArray]
    INSERT INTO @OrgUserIds (Id) SELECT [Id] FROM [dbo].[OrganizationUser] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    IF EXISTS (SELECT 1 FROM @OrgUserIds)
    BEGIN
        EXEC [dbo].[OrganizationUser_MigrateDefaultCollection] @OrgUserIds
    END

    DELETE CU FROM [dbo].[CollectionUser] CU INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId] WHERE OU.[UserId] IN (SELECT * FROM @ParsedIds)
    DELETE GU FROM [dbo].[GroupUser] GU INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId] WHERE OU.[UserId] IN (SELECT * FROM @ParsedIds)
    DELETE AP FROM [dbo].[AccessPolicy] AP INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[OrganizationUser] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[ProviderUser] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[SsoUser] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[EmergencyAccess] WHERE [GrantorId] IN (SELECT * FROM @ParsedIds) OR [GranteeId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[Send] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[NotificationStatus] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[Notification] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    DELETE FROM [dbo].[User] WHERE [Id] IN (SELECT * FROM @ParsedIds)

    COMMIT TRANSACTION User_DeleteById
END
GO
