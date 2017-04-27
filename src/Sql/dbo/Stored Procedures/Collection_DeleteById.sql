CREATE PROCEDURE [dbo].[Collection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Collection]
    WHERE
        [Id] = @Id
END