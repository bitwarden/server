CREATE PROCEDURE [dbo].[Installation_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Installation]
    WHERE
        [Id] = @Id
END