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
