CREATE PROCEDURE [dbo].[CollectionUser_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionUserView]
    WHERE
        [Id] = @Id
END