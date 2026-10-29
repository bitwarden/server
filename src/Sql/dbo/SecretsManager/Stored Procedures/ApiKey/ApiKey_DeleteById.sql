CREATE PROCEDURE [dbo].[ApiKey_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ApiKey]
    WHERE
        [Id] = @Id
END
