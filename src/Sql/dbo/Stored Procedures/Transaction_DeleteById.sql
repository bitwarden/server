CREATE PROCEDURE [dbo].[Transaction_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Transaction]
    WHERE
        [Id] = @Id
END