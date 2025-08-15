CREATE PROCEDURE [dbo].[Cipher_UpdateAttachment]
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
