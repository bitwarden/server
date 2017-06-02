CREATE PROCEDURE [dbo].[Device_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [Identifier] = @Identifier
END