CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.*,
        AR.Id as AuthRequestId,
        AR.CreationDate as AuthRequestCreationDa
    FROM dbo.DeviceView D
             LEFT JOIN (
        SELECT TOP 1 -- Take only the top record sorted by auth request creation date
                     Id,
                     CreationDate,
                     RequestDeviceIdentifier
        FROM dbo.AuthRequestView
        WHERE Type IN (0, 1) -- Include only AuthenticateAndUnlock and Unlock types, excluding Admin Approval (type 2)
          AND CreationDate >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE()) -- Ensure the request hasn't expired
          AND Approved IS NULL -- Include only requests that haven't been acknowledged or approved
        ORDER BY CreationDate DESC
    ) AR ON D.Identifier = AR.RequestDeviceIdentifier
    WHERE
        D.UserId = @UserId
      AND D.Active = 1; -- Include only active devices
END;
