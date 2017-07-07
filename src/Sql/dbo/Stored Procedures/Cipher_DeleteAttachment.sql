CREATE PROCEDURE [dbo].[Cipher_DeleteAttachment]
    @Id UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    DECLARE @Attachments NVARCHAR(MAX)
    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = [Attachments] 
    FROM 
        [dbo].[Cipher]
    WHERE [Id] = @Id

    DECLARE @AttachmentData NVARCHAR(MAX) = JSON_QUERY(@Attachments, @AttachmentIdPath)
    DECLARE @Size BIGINT = (CAST(JSON_VALUE(@AttachmentData, '$.Size') AS BIGINT) * -1)

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = JSON_MODIFY([Attachments], @AttachmentIdPath, NULL)
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId, @Size
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId, @Size
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END