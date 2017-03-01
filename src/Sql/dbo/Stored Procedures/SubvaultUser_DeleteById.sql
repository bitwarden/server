CREATE PROCEDURE [dbo].[SubvaultUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SubvaultUser]
    WHERE
        [Id] = @Id
END