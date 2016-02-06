CREATE PROCEDURE [dbo].[Folder_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [Id] = @Id
END
