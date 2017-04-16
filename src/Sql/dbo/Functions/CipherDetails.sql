CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.[Id],
    C.[UserId],
    C.[OrganizationId],
    C.[Type],
    C.[Data],
    C.[CreationDate],
    C.[RevisionDate],
    CASE WHEN
        C.[Favorites] IS NULL
        OR (
            SELECT TOP 1
                1
            FROM
                OPENJSON(C.[Favorites])
                WITH (
                    [Favorites_UserId] UNIQUEIDENTIFIER '$.u'
                )
            WHERE
                [Favorites_UserId] = @UserId
        ) IS NULL
    THEN 0
    ELSE 1
    END [Favorite],
    CASE WHEN
        C.[Folders] IS NULL
    THEN NULL
    ELSE (
        SELECT TOP 1
            [Folders_FolderId]
        FROM
            OPENJSON(C.[Folders])
            WITH (
                [Folders_UserId] UNIQUEIDENTIFIER '$.u',
                [Folders_FolderId] UNIQUEIDENTIFIER '$.f'
            )
        WHERE
            [Folders_UserId] = @UserId
    )
    END [FolderId]
FROM
    [dbo].[Cipher] C
