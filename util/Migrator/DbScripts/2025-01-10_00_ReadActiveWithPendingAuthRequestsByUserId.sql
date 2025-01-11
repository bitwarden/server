CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.*,
        AR.Id as AuthRequestId,
        AR.CreationDate as AuthRequestCreationDate
    FROM dbo.DeviceView D
    LEFT JOIN (
        SELECT 
            Id,
            CreationDate,
            RequestDeviceIdentifier,
            Approved,
            ROW_NUMBER() OVER (PARTITION BY RequestDeviceIdentifier ORDER BY CreationDate DESC) as rn
        FROM dbo.AuthRequestView
        WHERE Type IN (0, 1)  -- AuthenticateAndUnlock and Unlock types only
            AND CreationDate >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE()) -- Ensure the request hasn't expired
            AND UserId = @UserId --  Requests for this user only
    ) AR -- This join will get the most recent request per device, regardless of approval status 
    ON D.Identifier = AR.RequestDeviceIdentifier AND AR.rn = 1 AND AR.Approved IS NULL  -- Get only the most recent unapproved request per device
    WHERE
        D.UserId = @UserId -- Include only devices for this user
      AND D.Active = 1; -- Include only active devices
END;
