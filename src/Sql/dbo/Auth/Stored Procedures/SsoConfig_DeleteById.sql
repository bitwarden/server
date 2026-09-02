CREATE PROCEDURE [dbo].[SsoConfig_DeleteById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [Id] = @Id
END