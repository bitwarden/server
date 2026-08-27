CREATE PROCEDURE [dbo].[AccessRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The caller's open request for the cipher whose window can still be answered. A lapsed unanswered request is
    -- derived Expired: excluding it here un-blocks resubmission (SubmitAccessRequestCommand's duplicate guard) and
    -- keeps the client from showing a dead pending banner.
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
