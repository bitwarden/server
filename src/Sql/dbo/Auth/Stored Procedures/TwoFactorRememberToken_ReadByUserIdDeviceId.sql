CREATE PROCEDURE [dbo].[TwoFactorRememberToken_ReadByUserIdDeviceId]
    @UserId   UNIQUEIDENTIFIER,
    @DeviceId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TwoFactorRememberTokenView]
    WHERE
        [UserId] = @UserId
        AND [DeviceId] = @DeviceId
END
