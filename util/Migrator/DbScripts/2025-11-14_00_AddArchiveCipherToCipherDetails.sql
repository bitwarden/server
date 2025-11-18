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

IF OBJECT_ID('[dbo].[CipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[CipherDetails];
END
GO

CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.[Id],
    C.[UserId],
    C.[OrganizationId],
    C.[Type],
    C.[Data],
    C.[Attachments],
    C.[CreationDate],
    C.[RevisionDate],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Favorites] IS NULL
            OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
        THEN 0
        ELSE 1
    END [Favorite],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Folders] IS NULL
        THEN NULL
        ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
    END [FolderId],
    C.[DeletedDate],
    C.[Reprompt],
    C.[Key],
    CA.[ArchivedDate]
FROM
    [dbo].[Cipher] C
    LEFT JOIN [dbo].[CipherArchive] CA
        ON CA.[CipherId] = C.[Id]
        AND CA.[UserId] = @UserId;

GO

IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCipherDetails];
END
GO

CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
WITH [CTE] AS (
    SELECT
        [Id],
        [OrganizationId]
    FROM
        [OrganizationUser]
    WHERE
        [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
)
SELECT
    C.Id,
    C.UserId,
    C.OrganizationId,
    C.Type,
    C.Data,
    C.Attachments,
    C.CreationDate,
    C.RevisionDate,
    C.Favorite,
    C.FolderId,
    C.DeletedDate,
    C.ArchivedDate,
    C.Reprompt,
    C.[Key],
    CASE
        WHEN COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 1
        ELSE 0
    END [Edit],
    CASE
        WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 1
        ELSE 0
    END [ViewPassword],
    CASE
        WHEN COALESCE(CU.[Manage], CG.[Manage], 0) = 1
        THEN 1
        ELSE 0
    END [Manage],
    CASE
        WHEN O.[UseTotp] = 1
        THEN 1
        ELSE 0
    END [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) C
INNER JOIN
    [CTE] OU ON C.[UserId] IS NULL AND C.[OrganizationId] IN (SELECT [OrganizationId] FROM [CTE])
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId] AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
LEFT JOIN
    [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    CU.[CollectionId] IS NOT NULL
    OR CG.[CollectionId] IS NOT NULL

UNION ALL

SELECT
    C.Id,
    C.UserId,
    C.OrganizationId,
    C.Type,
    C.Data,
    C.Attachments,
    C.CreationDate,
    C.RevisionDate,
    C.Favorite,
    C.FolderId,
    C.DeletedDate,
    C.ArchivedDate,
    C.Reprompt,
    C.[Key],
    1 [Edit],
    1 [ViewPassword],
    1 [Manage],
    0 [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) AS C
WHERE
    C.[UserId] = @UserId;
END
GO
