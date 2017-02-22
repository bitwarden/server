CREATE PROCEDURE [dbo].[Share_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Share]
    WHERE
        [Id] = @Id
END
