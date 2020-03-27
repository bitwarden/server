/**
 * Soft Delete for Cipher Table
 * @WARN: May require new indexes/re-indexing, depending on scale/usage.
 */
IF COL_LENGTH('[dbo].[Cipher]', 'DeletedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Cipher]
        ADD [DeletedDate] DATETIME2 (7) NULL;
END
GO

IF OBJECT_ID('[dbo].[CipherView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherView]';
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
    C.[DeletedDate]
FROM
    [dbo].[Cipher] C
GO

IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserCipherDetails]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId];
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Deleted BIT
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
        AND
        (
            (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
            OR (@Deleted = 0 AND [DeletedDate] IS NULL)
        )
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_ReadByOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Deleted BIT
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
        AND
        (
            (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
            OR (@Deleted = 0 AND [DeletedDate] IS NULL)
        )
END
GO

IF OBJECT_ID('[dbo].[CipherOrganizationDetails_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherOrganizationDetails_ReadById];
END
GO

CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadById]
    @Id UNIQUEIDENTIFIER,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherView] C
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.[Id] = @Id
        AND
        (
            (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
            OR (@Deleted = 0 AND [DeletedDate] IS NULL)
        )
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserId];
END
GO

CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
        OR (@Deleted = 0 AND [DeletedDate] IS NULL)
END
GO

IF OBJECT_ID('[dbo].[Cipher_Delete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Delete];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Delete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER,
    @Permanent AS BIT
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [Attachments] BIT NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId],
        CASE WHEN [Attachments] IS NULL THEN 0 ELSE 1 END
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    IF @Permanent = 1
    BEGIN
        DELETE
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] IN (SELECT [Id] FROM #Temp)
    END
    ELSE
    BEGIN
        UPDATE
            [dbo].[Cipher]
        SET
            [DeletedDate] = SYSUTCDATETIME()
        WHERE
            [Id] IN (SELECT [Id] FROM #Temp)
    END

    -- Cleanup orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        -- Storage cleanup for groups only matters if we're permanently deleting
        IF @Permanent = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrgId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Cleanup user
    IF @Permanent = 1
    BEGIN
        -- Storage cleanup for users only matters if we're permanently deleting
        DECLARE @UserCiphersWithStorageCount INT
        SELECT
            @UserCiphersWithStorageCount = COUNT(1)
        FROM
            #Temp
        WHERE
            [UserId] IS NOT NULL
            AND [Attachments] = 1

        IF @UserCiphersWithStorageCount > 0
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
    END
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteById];
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @Permanent AS BIT
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Attachments BIT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = CASE WHEN [Attachments] IS NOT NULL THEN 1 ELSE 0 END
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id
        
    IF @Permanent = 1
    BEGIN
        DELETE
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] = @Id
    END
    ELSE
    BEGIN
        UPDATE
            [dbo].[Cipher]
        SET
            [DeletedDate] = SYSUTCDATETIME()
        WHERE
            [Id] = @Id
    END

    IF @OrganizationId IS NOT NULL
    BEGIN
        IF @Attachments = 1 AND @Permanent = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        IF @Attachments = 1 AND @Permanent = 1
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_Create]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Create]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_Move]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Move]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadCanEditByIdUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_ReadCanEditByIdUserId]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_Update]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Update]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdatePartial]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_UpdatePartial]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_Create]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_Create]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_Update]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_Update]';
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_Create]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionCipher_Create]';
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_Delete]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionCipher_Delete]';
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollections]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionCipher_UpdateCollections]';
END
GO

IF OBJECT_ID('[dbo].[Folder_DeleteById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Folder_DeleteById]';
END
GO

IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Organization_DeleteById]';
END
GO

IF OBJECT_ID('[dbo].[Organization_UpdateStorage]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Organization_UpdateStorage]';
END
GO

IF OBJECT_ID('[dbo].[User_DeleteById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[User_DeleteById]';
END
GO

IF OBJECT_ID('[dbo].[User_UpdateStorage]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[User_UpdateStorage]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_CreateWithCollections]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_CreateWithCollections]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_CreateWithCollections]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_CreateWithCollections]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByIdUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_ReadByIdUserId]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteAttachment]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_DeleteAttachment]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_DeleteByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteByUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_DeleteByUserId]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateAttachment]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_UpdateAttachment]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateWithCollections]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_UpdateWithCollections]';
END
GO

DROP INDEX IF EXISTS [IX_Cipher_UserId_OrganizationId_IncludeAll]
    ON [dbo].[Cipher];
GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_OrganizationId_IncludeAll]
    ON [dbo].[Cipher]([UserId] ASC, [OrganizationId] ASC)
    INCLUDE([Type], [Data], [Favorites], [Folders], [Attachments], [CreationDate], [RevisionDate], [DeletedDate]);
GO
