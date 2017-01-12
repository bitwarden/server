CREATE PROCEDURE [dbo].[Device_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [Id] = @Id
END