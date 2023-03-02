CREATE PROCEDURE [dbo].[Cipher_UpdateAttachment]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50),
    @AttachmentData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = 
            CASE
            WHEN [Attachments] IS NULL THEN
                CONCAT('{', @AttachmentIdKey, ':', @AttachmentData, '}')
            ELSE
                JSON_MODIFY([Attachments], @AttachmentIdPath, JSON_QUERY(@AttachmentData, '$'))
            END
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