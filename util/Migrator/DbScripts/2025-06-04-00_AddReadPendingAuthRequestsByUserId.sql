-- Adds a stored procedure to read pending authentication requests by user ID.
CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_ReadPendingByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PendingRequests AS (
        SELECT
            AR.*,
            D.Id AS DeviceId,
            ROW_NUMBER() OVER (PARTITION BY AR.RequestDeviceIdentifier ORDER BY AR.CreationDate DESC) AS rn
        FROM dbo.AuthRequestView AR
        LEFT JOIN
        	Device D ON AR.RequestDeviceIdentifier = D.Identifier
        WHERE AR.Type IN (0, 1) -- 0 = AuthenticateAndUnlock, 1 = Unlock
            AND AR.CreationDate >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE())
            AND AR.UserId = @UserId
    )
    SELECT PR.*
    FROM PendingRequests PR
    WHERE rn = 1
    AND PR.Approved IS NULL;
END;
