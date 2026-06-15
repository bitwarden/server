CREATE PROCEDURE [dbo].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's approved-but-not-yet-activated request whose window can still produce access. Future windows are
    -- included (the client shows the upcoming window); lapsed windows are excluded so the client never offers an
    -- activation that the server would reject. A request that has produced a lease is activated, not approved.
    -- Extension requests are excluded: an approved extension pushes its parent lease's end out in place and never
    -- produces a lease of its own, so it must not surface here as an activatable "Start access" request.
    SELECT TOP 1
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[CipherId] = @CipherId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] > @Now
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    ORDER BY
        AR.[CreationDate] DESC
END
