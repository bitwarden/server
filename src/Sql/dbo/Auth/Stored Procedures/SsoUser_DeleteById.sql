CREATE PROCEDURE [dbo].[SsoUser_DeleteById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [Id] = @Id
END
