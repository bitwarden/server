CREATE PROCEDURE [dbo].[Group_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Group]
    WHERE
        [Id] = @Id
END