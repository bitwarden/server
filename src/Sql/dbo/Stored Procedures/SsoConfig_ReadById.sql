CREATE PROCEDURE [dbo].[SsoConfig_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [Id] = @Id
END
