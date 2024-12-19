CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SELECT
        D.*,
        AR.Id as AuthRequestId,
        AR.CreationDate as AuthRequestCreationDate
    FROM [dbo].[DeviceView] D
             LEFT OUTER JOIN (
        SELECT TOP 1 -- Take only the top record sorted by auth request creation date
                     AR.Id,
                     AR.CreationDate,
                     AR.RequestDeviceIdentifier
        FROM [dbo].[AuthRequestView] AR
        WHERE AR.Type IN (0, 1) -- Include only AuthenticateAndUnlock and Unlock types, excluding Admin Approval (type 2)
          AND DATEADD(mi, @ExpirationMinutes, AR.CreationDate) > GETUTCDATE() -- Ensure the request hasn't expired
          AND AR.Approved IS NULL -- Include only requests that haven't been acknowledged or approved
        ORDER BY AR.CreationDate DESC
    ) AR ON D.Identifier = AR.RequestDeviceIdentifier
    WHERE
        D.UserId = @UserId
      AND D.Active = 1 -- Include only active devices
END
