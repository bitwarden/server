CREATE PROCEDURE [dbo].[Cipher_UpdatePartial]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @FavoritesJson VARCHAR(MAX) = NULL
    DECLARE @FoldersJson VARCHAR(MAX) = NULL

    SELECT
        @FavoritesJson = [Favorites],
        @FoldersJson = [Folders]
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    DECLARE @ExistingFolderId UNIQUEIDENTIFIER = NULL

    -- NOTE
    -- JSON_MODIFY operations involving @Existing__Index may be subject to race conditions involving that index/key.
    -- Look for a better approach, like removing objects by conditions.
    DECLARE @ExistingFolderIndex INT = NULL
    DECLARE @ExistingFavoriteIndex INT = NULL

    IF @FoldersJson IS NOT NULL
    BEGIN
        SELECT TOP 1
            @ExistingFolderId = JSON_VALUE([Value], '$.f'),
            @ExistingFolderIndex = [Key]
        FROM (
            SELECT
                [Key],
                [Value]
            FROM
                OPENJSON(@FoldersJson)
        ) [Results]
        WHERE JSON_VALUE([Value], '$.u') = @UserId
    END

    IF @FavoritesJson IS NOT NULL
    BEGIN
        SELECT TOP 1
            @ExistingFavoriteIndex = [Key]
        FROM (
            SELECT
                [Key],
                [Value]
            FROM
                OPENJSON(@FavoritesJson)
        ) [Results]
        WHERE JSON_VALUE([Value], '$.u') = @UserId
    END

    -- ----------------------------
    -- Update [Folders]
    -- ----------------------------

    IF @ExistingFolderId IS NOT NULL AND @FolderId IS NULL
    BEGIN
        -- User had an existing folder, but now they have removed the folder assignment.
        -- Remove the index of the existing folder object from the [Folders] JSON array.

        UPDATE
            [dbo].[Cipher]
        SET
            -- TODO: How to remove index?
            [Folders] = JSON_MODIFY([Folders], CONCAT('$[', @ExistingFolderIndex, ']'), NULL)
        WHERE
            [Id] = @Id
    END
    ELSE IF @FolderId IS NOT NULL
    BEGIN
        IF @FoldersJson IS NULL
        BEGIN
            -- [Folders] has no existing JSON data.
            -- Set @FolderId (with @UserId) as a new JSON object for index 0 of an array.

            UPDATE
                [dbo].[Cipher]
            SET
                [Folders] = JSON_QUERY((SELECT @UserId u, @FolderId f FOR JSON PATH))
            WHERE
                [Id] = @Id
        END
        ELSE IF @ExistingFolderId IS NULL
        BEGIN
            -- [Folders] has some existing JSON data, but the user had no existing folder.
            -- Append @FolderId (with @UserId) as a new JSON object.

            UPDATE
                [dbo].[Cipher]
            SET
                [Folders] = JSON_MODIFY([Folders], 'append $',
                    JSON_QUERY((SELECT @UserId u, @FolderId f FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)))
            WHERE
                [Id] = @Id
        END
        ELSE IF @FolderId != @ExistingFolderId
        BEGIN
            -- User had an existing folder assignemnt, but have changed the assignment to another folder.
            -- Update the index of the existing folder object from the [Folders] JSON array to to include the new @FolderId

            UPDATE
                [dbo].[Cipher]
            SET
                [Folders] = JSON_MODIFY([Folders], CONCAT('$[', @ExistingFolderIndex, ']'),
                    JSON_QUERY((SELECT @UserId u, @FolderId f FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)))
            WHERE
                [Id] = @Id
        END
    END

    -- ----------------------------
    -- Update [Favorites]
    -- ----------------------------

    IF @Favorite = 0 AND @ExistingFavoriteIndex IS NOT NULL
    BEGIN
        -- User had the cipher marked as a favorite, but now it is not.
        -- Remove the index of the existing user object from the [Favorites] JSON array.

        UPDATE
            [dbo].[Cipher]
        SET
            -- TODO: How to remove index?
            [Favorites] = JSON_MODIFY([Favorites], CONCAT('$[', @ExistingFavoriteIndex, ']'), NULL)
        WHERE
            [Id] = @Id
    END
    ELSE IF @Favorite = 1 AND @FavoritesJson IS NULL
    BEGIN
        -- User is marking the cipher as a favorite and there is no existing JSON data
        -- Set @UserId as a new JSON object for index 0 of an array.

        UPDATE
            [dbo].[Cipher]
        SET
            [Favorites] = JSON_QUERY((SELECT @UserId u FOR JSON PATH))
        WHERE
            [Id] = @Id
    END
    ELSE IF @Favorite = 1 AND @ExistingFavoriteIndex IS NULL
    BEGIN
        -- User is marking the cipher as a favorite whenever it previously was not.
        -- Append @UserId as a new JSON object.

        UPDATE
            [dbo].[Cipher]
        SET
            [Favorites] = JSON_MODIFY([Favorites], 'append $',
                JSON_QUERY((SELECT @UserId u FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)))
        WHERE
            [Id] = @Id
    END
END