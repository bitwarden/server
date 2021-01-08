CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [Status] = 3
    AND
        DATEADD(DAY, [WaitTimeDays], [RecoveryInitiatedDate]) <= GETUTCDATE()
END