IF OBJECT_ID('[dbo].[CipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[CipherDetails]
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
    END [FolderId]
FROM
    [dbo].[Cipher] C
GO

IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCipherDetails]
END
GO

CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
WITH [CTE] AS (
    SELECT
        [Id],
        [OrganizationId],
        [AccessAll]
    FROM
        [OrganizationUser]
    WHERE
        [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
)
SELECT
    C.*,
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR CU.[ReadOnly] = 0
            OR G.[AccessAll] = 1
            OR CG.[ReadOnly] = 0
        THEN 1
        ELSE 0
    END [Edit],
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
    [dbo].[Organization] O ON O.[Id] = OU.OrganizationId AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
LEFT JOIN
    [dbo].[CollectionCipher] CC ON OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[AccessAll] = 1
    OR CU.[CollectionId] IS NOT NULL
    OR G.[AccessAll] = 1
    OR CG.[CollectionId] IS NOT NULL

UNION ALL

SELECT
    *,
    1 [Edit],
    0 [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId)
WHERE
    [UserId] = @UserId
GO

IF EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Cipher_OrganizationId_Type'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    DROP INDEX [IX_Cipher_OrganizationId_Type] ON [dbo].[Cipher]
END
GO

IF EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Cipher_UserId_Type_IncludeAll'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    DROP INDEX [IX_Cipher_UserId_Type_IncludeAll] ON [dbo].[Cipher]
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Cipher_UserId_OrganizationId_IncludeAll'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_OrganizationId_IncludeAll]
        ON [dbo].[Cipher]([UserId] ASC, [OrganizationId] ASC)
        INCLUDE ([Type], [Data], [Favorites], [Folders], [Attachments], [CreationDate], [RevisionDate])
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Cipher_OrganizationId'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Cipher_OrganizationId]
        ON [dbo].[Cipher]([OrganizationId] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_GroupUser_OrganizationUserId'
    AND object_id = OBJECT_ID('[dbo].[GroupUser]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GroupUser_OrganizationUserId]
        ON [dbo].[GroupUser]([OrganizationUserId] ASC)
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Organization_Enabled'
    AND object_id = OBJECT_ID('[dbo].[Organization]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_Enabled]
        ON [dbo].[Organization]([Id] ASC, [Enabled] ASC)
        INCLUDE ([UseTotp])
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Collection_OrganizationId_IncludeAll'
    AND object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Collection_OrganizationId_IncludeAll]
        ON [dbo].[Collection]([OrganizationId] ASC)
        INCLUDE([CreationDate], [Name], [RevisionDate])
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByTypeUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByTypeUserId]
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserIdHasCollection]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasCollection]
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *,
        1 [Edit],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    WHERE
        [UserId] = @UserId
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Organization_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[UserId] IS NULL
        AND C.[OrganizationId] = @Id

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
