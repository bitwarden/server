CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId]
    @EmergencyAccessId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[EmergencyAccess] EA ON EA.[GranteeId] = U.[Id]
    WHERE
        EA.[Id] = @EmergencyAccessId
        AND EA.[Status] = 2 -- Confirmed
END