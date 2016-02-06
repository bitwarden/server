CREATE PROCEDURE [dbo].[Site_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE
    FROM
        [dbo].[Site]
    WHERE
        [Id] = @Id
END
