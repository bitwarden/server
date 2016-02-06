CREATE PROCEDURE [dbo].[Folder_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    BEGIN TRANSACTION Folder_DeleteById

    UPDATE
        [dbo].[Site]
    SET
        [FolderId] = NULL
    WHERE
        [FolderId] = @Id

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Folder_DeleteById
END
