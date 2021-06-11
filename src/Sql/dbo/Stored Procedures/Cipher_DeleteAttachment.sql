CREATE PROCEDURE [dbo].[Cipher_DeleteAttachment]
    @Id UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @UserId = [UserId],
        @OrganizationId = [OrganizationId]
    FROM 
        [dbo].[Cipher]
    WHERE [Id] = @Id

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = JSON_MODIFY([Attachments], @AttachmentIdPath, NULL)
    WHERE
        [Id] = @Id

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
