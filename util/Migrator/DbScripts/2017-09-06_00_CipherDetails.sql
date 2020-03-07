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
    CASE WHEN
        @UserId IS NULL
        OR C.[Favorites] IS NULL
        OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
    THEN 0
    ELSE 1
    END [Favorite],
    CASE WHEN
        @UserId IS NULL
        OR C.[Folders] IS NULL
    THEN NULL
    ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
    END [FolderId]
FROM
    [dbo].[Cipher] C
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadById]
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        1 [Edit],
        CASE 
            WHEN O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](NULL) C
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.[Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserIdHasCollection]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasCollection]
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasCollection]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
            ELSE 0
        END [Edit],
        CASE 
            WHEN C.[UserId] IS NULL AND O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadCanEditByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId]
END
GO

CREATE PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanEdit BIT

    ;WITH [CTE] AS (
        SELECT
            CASE 
                WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
                ELSE 0
            END [Edit]
        FROM
            [dbo].[Cipher] C
        LEFT JOIN
            [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
        LEFT JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
        LEFT JOIN
            [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            C.Id = @Id
            AND (
                C.[UserId] = @UserId
                OR (
                    C.[UserId] IS NULL
                    AND OU.[Status] = 2 -- 2 = Confirmed
                    AND O.[Enabled] = 1
                    AND (
                        OU.[AccessAll] = 1
                        OR CU.[CollectionId] IS NOT NULL
                        OR G.[AccessAll] = 1
                        OR CG.[CollectionId] IS NOT NULL
                    )
                )
            )
    )
    SELECT
        @CanEdit = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [CTE]
    WHERE
        [Edit] = 1

    SELECT @CanEdit
END
GO
