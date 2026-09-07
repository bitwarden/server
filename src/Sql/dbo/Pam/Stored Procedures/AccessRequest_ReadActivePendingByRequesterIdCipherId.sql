CREATE PROCEDURE [dbo].[AccessRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Caller's open request for the cipher; a lapsed one derives Expired, allowing resubmission.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessRequest]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Action] = 0 -- None (open)
        AND [NotAfter] > @Now
    ORDER BY
        [CreationDate] DESC
END
