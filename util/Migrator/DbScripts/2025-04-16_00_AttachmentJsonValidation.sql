CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateAttachment]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50),
    @AttachmentData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Validate that AttachmentData is valid JSON
    IF ISJSON(@AttachmentData) = 0
    BEGIN
        THROW 50000, 'Invalid JSON format in AttachmentData parameter', 1;
        RETURN;
    END

    -- Validate that AttachmentData has the expected structure
    -- Check for required fields
    IF JSON_VALUE(@AttachmentData, '$.FileName') IS NULL OR
       JSON_VALUE(@AttachmentData, '$.Size') IS NULL
    BEGIN
        THROW 50000, 'AttachmentData is missing required fields (FileName, Size)', 1;
        RETURN;
    END

    -- Validate data types for critical fields
    DECLARE @Size BIGINT = TRY_CAST(JSON_VALUE(@AttachmentData, '$.Size') AS BIGINT)
    IF @Size IS NULL OR @Size <= 0
    BEGIN
        THROW 50000, 'AttachmentData has invalid Size value', 1;
        RETURN;
    END

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)
    DECLARE @NewAttachments NVARCHAR(MAX)

    -- Get current attachments
    DECLARE @CurrentAttachments NVARCHAR(MAX)
    SELECT @CurrentAttachments = [Attachments] FROM [dbo].[Cipher] WHERE [Id] = @Id

    -- Prepare the new attachments value based on current state
    IF @CurrentAttachments IS NULL
    BEGIN
        -- Create new JSON object with the attachment
        SET @NewAttachments = CONCAT('{', @AttachmentIdKey, ':', @AttachmentData, '}')

        -- Validate the constructed JSON
        IF ISJSON(@NewAttachments) = 0
        BEGIN
            THROW 50000, 'Failed to create valid JSON when adding new attachment', 1;
            RETURN;
        END
    END
    ELSE
    BEGIN
        -- Validate existing attachments
        IF ISJSON(@CurrentAttachments) = 0
        BEGIN
            THROW 50000, 'Current attachments data is not valid JSON', 1;
            RETURN;
        END

        -- Modify existing JSON
        SET @NewAttachments = JSON_MODIFY(@CurrentAttachments, @AttachmentIdPath, JSON_QUERY(@AttachmentData, '$'))

        -- Validate the modified JSON
        IF ISJSON(@NewAttachments) = 0
        BEGIN
            THROW 50000, 'Failed to create valid JSON when updating existing attachments', 1;
            RETURN;
        END
    END

    -- Update with validated JSON
    UPDATE [dbo].[Cipher]
    SET [Attachments] = @NewAttachments
    WHERE [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_DeleteAttachment]
    @Id UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @CurrentAttachments NVARCHAR(MAX)
    DECLARE @NewAttachments NVARCHAR(MAX)

    -- Get current cipher data
    SELECT
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @CurrentAttachments = [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE [Id] = @Id

    -- If there are no attachments, nothing to do
    IF @CurrentAttachments IS NULL
    BEGIN
        RETURN;
    END

    -- Validate the initial JSON
    IF ISJSON(@CurrentAttachments) = 0
    BEGIN
        THROW 50000, 'Current initial attachments data is not valid JSON', 1;
        RETURN;
    END

    -- Check if the attachment exists before trying to remove it
    IF JSON_QUERY(@CurrentAttachments, @AttachmentIdPath) IS NULL
    BEGIN
        -- Attachment doesn't exist, nothing to do
        RETURN;
    END

    -- Create the new attachments JSON with the specified attachment removed
    SET @NewAttachments = JSON_MODIFY(@CurrentAttachments, @AttachmentIdPath, NULL)

    -- Validate the resulting JSON
    IF ISJSON(@NewAttachments) = 0
    BEGIN
        THROW 50000, 'Failed to create valid JSON when removing attachment', 1;
        RETURN;
    END

    -- Check if we've removed all attachments and have an empty object
    IF @NewAttachments = '{}'
    BEGIN
        -- If we have an empty JSON object, set to NULL instead
        SET @NewAttachments = NULL;
    END

    -- Update with validated JSON
    UPDATE [dbo].[Cipher]
    SET [Attachments] = @NewAttachments
    WHERE [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

-- Remove [Attachments] assignment from Cipher_Create, Cipher_Update, and CipherDetails_Update procedures

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
    @Key VARCHAR(MAX) = NULL
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
        [Key]
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
        @Key
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

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_Update]
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
    @DeletedDate DATETIME2(2),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL
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

CREATE OR ALTER PROCEDURE [dbo].[Cipher_Update]
    @Id UNIQUEIDENTIFIER,
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
