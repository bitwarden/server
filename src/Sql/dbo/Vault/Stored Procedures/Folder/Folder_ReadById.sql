CREATE PROCEDURE [dbo].[Folder_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [Id] = @Id
END