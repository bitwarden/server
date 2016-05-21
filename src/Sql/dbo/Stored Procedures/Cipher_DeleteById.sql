CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Cipher_DeleteById

    UPDATE
        [dbo].[Cipher]
    SET
        [FolderId] = NULL
    WHERE
        [FolderId] = @Id

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Cipher_DeleteById
END
