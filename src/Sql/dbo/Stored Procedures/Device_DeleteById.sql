CREATE PROCEDURE [dbo].[Device_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [Id] = @Id
END