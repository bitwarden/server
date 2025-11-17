IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserId];
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Id,
        c.UserId,
        c.OrganizationId,
        c.Type,
        c.Data,
        c.Favorites,
        c.Folders,
        c.Attachments,
        c.CreationDate,
        c.RevisionDate,
        c.DeletedDate,
        c.Reprompt,
        c.Key,
        ca.ArchivedDate
    FROM
        [dbo].[UserCipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId;
END
GO


IF OBJECT_ID('[dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId];
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        c.Id,
        c.UserId,
        c.OrganizationId,
        c.Type,
        c.Data,
        c.Favorites,
        c.Folders,
        c.Attachments,
        c.CreationDate,
        c.RevisionDate,
        c.DeletedDate,
        c.Reprompt,
        c.Key,
        1 [Edit],
        1 [ViewPassword],
        1 [Manage],
        0 [OrganizationUseTotp],
        ca.ArchivedDate
    FROM
        [dbo].[CipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId
    WHERE
        c.[UserId] = @UserId
END
GO

IF OBJECT_ID('[dbo].[Cipher_Archive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Archive];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Archive]
    @Ids dbo.GuidIdArray READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    WITH CipherIdsToArchive AS
    (
        SELECT DISTINCT C.Id
        FROM [dbo].[Cipher] C
        INNER JOIN @Ids I ON C.Id = I.[Id]
        WHERE (C.[UserId] = @UserId)
    )
    INSERT INTO [dbo].[CipherArchive] (CipherId, UserId, ArchivedDate)
    SELECT Cta.Id, @UserId, @UtcNow
    FROM CipherIdsToArchive Cta
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[CipherArchive] Ca
        WHERE Ca.CipherId = Cta.Id
          AND Ca.UserId = @UserId
    );

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    SELECT @UtcNow
END

GO

IF OBJECT_ID('[dbo].[Cipher_Unarchive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Unarchive];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Unarchive]
    @Ids dbo.GuidIdArray READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    DELETE Ca
    FROM [dbo].[CipherArchive] Ca
    INNER JOIN @Ids I ON Ca.CipherId = I.[Id]
    WHERE Ca.UserId = @UserId

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    SELECT @UtcNow;
END
GO


IF OBJECT_ID('[dbo].[Cipher_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Update];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Update]
    @Id UNIQUEIDENTIFIER,
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
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Favorites] = @Favorites,
        [Folders] = @Folders,
        [Attachments] = @Attachments,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate,
        [Reprompt] = @Reprompt,
        [Key] = @Key
    WHERE
        [Id] = @Id

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
