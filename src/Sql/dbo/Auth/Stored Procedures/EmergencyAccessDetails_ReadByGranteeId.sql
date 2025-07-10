CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGranteeId]
    @GranteeId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GranteeId] = @GranteeId
END