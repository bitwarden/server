CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGrantorId]
    @GrantorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GrantorId] = @GrantorId
END