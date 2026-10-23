IF COL_LENGTH('[dbo].[Cipher]', 'ReportExclusions') IS NULL
BEGIN
    ALTER TABLE [dbo].[Cipher]
        ADD [ReportExclusions] NVARCHAR(MAX) NULL;
END
GO

CREATE OR ALTER FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
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
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Archives] IS NULL
        THEN NULL
        ELSE TRY_CONVERT(DATETIME2(7), JSON_VALUE(C.[Archives], CONCAT('$."', @UserId, '"')))
    END [ArchivedDate],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[ReportExclusions] IS NULL
            OR JSON_VALUE(C.[ReportExclusions], CONCAT('$."', @UserId, '"')) IS NULL
        THEN 0
        ELSE 1
    END [ExcludedFromReports]
FROM
    [dbo].[Cipher] C;
GO

IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserCipherDetails]';
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @Archives NVARCHAR(MAX) = NULL,
    @ReportExclusions NVARCHAR(MAX) = NULL -- not used
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [CreationDate],
        [RevisionDate],
        [DeletedDate],
        [Reprompt],
        [Key],
        [Archives]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @CreationDate,
        @RevisionDate,
        @DeletedDate,
        @Reprompt,
        @Key,
        @Archives
    )

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

CREATE OR ALTER PROCEDURE [dbo].[Cipher_Update]
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
    @Key VARCHAR(MAX) = NULL,
    @Archives NVARCHAR(MAX) = NULL,
    @ReportExclusions NVARCHAR(MAX) = NULL -- not used
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
        [Key] = @Key,
        [Archives] = @Archives
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

CREATE OR ALTER PROCEDURE [dbo].[Cipher_CreateWithCollections]
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
    @Key VARCHAR(MAX) = NULL,
    @Archives NVARCHAR(MAX) = NULL,
    @ReportExclusions NVARCHAR(MAX) = NULL, -- not used
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @DeletedDate, @Reprompt, @Key, @Archives

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    -- Bump the account revision date AFTER collections are assigned.
    IF @UpdateCollectionsSuccess = 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateWithCollections]
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
    @Key VARCHAR(MAX) = NULL,
    @Archives NVARCHAR(MAX) = NULL,
    @ReportExclusions NVARCHAR(MAX) = NULL, -- not used
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Cipher_UpdateWithCollections

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    IF @UpdateCollectionsSuccess < 0
    BEGIN
        COMMIT TRANSACTION Cipher_UpdateWithCollections
        SELECT -1 -- -1 = Failure
        RETURN
    END

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [Attachments] = @Attachments,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate,
        [Key] = @Key,
        [Folders] = @Folders,
        [Favorites] = @Favorites,
        [Reprompt] = @Reprompt,
        [Archives] = @Archives
        -- No need to update CreationDate or Type since that data will not change
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Cipher_UpdateWithCollections

    IF @Attachments IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_UpdateStorage] @UserId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SELECT 0 -- 0 = Success
END
GO

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @ViewPassword BIT, -- not used
    @Manage BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @ArchivedDate DATETIME2(7) = NULL,
    @Archives NVARCHAR(MAX) = NULL, -- not used
    @ExcludedFromReports BIT = NULL, -- not used
    @ReportExclusions NVARCHAR(MAX) = NULL -- not used
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [CreationDate],
        [RevisionDate],
        [DeletedDate],
        [Reprompt],
        [Key],
        [Archives]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        CASE WHEN @Favorite = 1 THEN CONCAT('{', @UserIdKey, ':true}') ELSE NULL END,
        CASE WHEN @FolderId IS NOT NULL THEN CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}') ELSE NULL END,
        @CreationDate,
        @RevisionDate,
        @DeletedDate,
        @Reprompt,
        @Key,
        CASE WHEN @ArchivedDate IS NOT NULL THEN CONCAT('{', @UserIdKey, ':"', CONVERT(NVARCHAR(30), @ArchivedDate, 127), '"}') ELSE NULL END
    )

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

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @ViewPassword BIT, -- not used
    @Manage BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @ArchivedDate DATETIME2(7) = NULL,
    @Archives NVARCHAR(MAX) = NULL, -- not used
    @ExcludedFromReports BIT = NULL, -- not used
    @ReportExclusions NVARCHAR(MAX) = NULL, -- not used
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[CipherDetails_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @FolderId, @Favorite, @Edit, @ViewPassword, @Manage,
        @OrganizationUseTotp, @DeletedDate, @Reprompt, @Key, @ArchivedDate

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    -- Bump the account revision date AFTER collections are assigned.
    IF @UpdateCollectionsSuccess = 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @ViewPassword BIT, -- not used
    @Manage BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(2),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @ArchivedDate DATETIME2(7) = NULL,
    @Archives NVARCHAR(MAX) = NULL, -- not used
    @ExcludedFromReports BIT = NULL, -- not used
    @ReportExclusions NVARCHAR(MAX) = NULL -- not used
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Folders] =
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END,
        [Favorites] =
            CASE
            WHEN @Favorite = 1 AND [Favorites] IS NULL THEN
                CONCAT('{', @UserIdKey, ':true}')
            WHEN @Favorite = 1 THEN
                JSON_MODIFY([Favorites], @UserIdPath, CAST(1 AS BIT))
            ELSE
                JSON_MODIFY([Favorites], @UserIdPath, NULL)
            END,
        [Archives] =
            CASE
            WHEN @ArchivedDate IS NOT NULL AND [Archives] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', CONVERT(NVARCHAR(30), @ArchivedDate, 127), '"}')
            WHEN @ArchivedDate IS NOT NULL THEN
                JSON_MODIFY([Archives], @UserIdPath, CONVERT(NVARCHAR(30), @ArchivedDate, 127))
            ELSE
                JSON_MODIFY([Archives], @UserIdPath, NULL)
            END,
        [Attachments] = @Attachments,
        [Reprompt] = @Reprompt,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate,
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

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate],
        [ExcludedFromReports],
        MAX ([Edit]) AS [Edit],
        MAX ([ViewPassword]) AS [ViewPassword],
        MAX ([Manage]) AS [Manage]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Id] = @Id
    GROUP BY
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate],
        [ExcludedFromReports]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_ExcludeFromReports]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        C
    SET
        [ReportExclusions] = JSON_MODIFY(
            COALESCE(C.[ReportExclusions], N'{}'),
            CONCAT('$."', @UserId, '"'),
            CAST(1 AS BIT)
        )
    FROM
        [dbo].[Cipher] C
        INNER JOIN @Ids I ON I.[Id] = C.[Id]
    WHERE
        C.[UserId] = @UserId
        AND C.[OrganizationId] IS NULL
        AND C.[Type] = 1 -- Login
        AND C.[DeletedDate] IS NULL

    DECLARE @Count INT = @@ROWCOUNT

    IF @Count > 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END

    SELECT @Count
END
GO

EXECUTE sp_refreshview N'[dbo].[CipherView]'
GO
