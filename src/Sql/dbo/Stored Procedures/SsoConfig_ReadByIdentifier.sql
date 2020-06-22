CREATE PROCEDURE [dbo].[SsoConfig_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [Identifier] = @Identifier
END
