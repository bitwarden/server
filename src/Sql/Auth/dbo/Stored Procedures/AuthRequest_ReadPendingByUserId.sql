CREATE PROCEDURE [dbo].[AuthRequest_ReadPendingByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PendingRequests AS (
        SELECT
            AR.*,
            ROW_NUMBER() OVER (PARTITION BY RequestDeviceIdentifier ORDER BY CreationDate DESC) AS rn
        FROM dbo.AuthRequestView AR
        WHERE Type IN (0, 1)
            AND AR.CreationDate >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE())
            AND AR.UserId = @UserId
            AND AR.Approved IS NULL
    )
    SELECT PR.*
    FROM PendingRequests PR
    WHERE rn = 1;
END;
