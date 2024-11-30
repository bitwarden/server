CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
SELECT  D.*,
        AR.Id as AuthRequestId,
        AR.CreationDate as AuthRequestCreationDate
FROM [dbo].[DeviceView] D
    LEFT OUTER JOIN [dbo].[AuthRequestView] AR
ON D.userId = AR.userId
    AND AR.RequestDeviceIdentifier = D.Identifier
    AND AR.Type IN (0, 1) -- Exclude Admin Approval (type 2)
    AND DATEADD(mi, @ExpirationMinutes, AR.CreationDate) < GETUTCDATE() -- This means it hasn't expired
    AND AR.Approved IS NOT NULL -- This means it hasn't been approved already
WHERE D.UserId = @UserId
  AND D.Active = 1 -- Device is active
END
