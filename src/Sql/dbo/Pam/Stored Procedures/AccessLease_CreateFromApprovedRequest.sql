CREATE PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Activation of an approved request: mints the active lease that authorizes access, spanning the request's
    -- approved window. Every application-level precondition is re-checked inside the INSERT so a concurrent
    -- activation cannot double-mint; zero rows inserted means a precondition no longer held and the caller decides
    -- how to surface that. [IX_AccessLease_AccessRequestId] (unique) is the backstop when two calls pass the
    -- NOT EXISTS check simultaneously.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* Active */, AR.[NotBefore], AR.[NotAfter], NULL, NULL, @Now
    FROM [dbo].[AccessRequest] AR
    WHERE
        AR.[Id] = @AccessRequestId
        AND AR.[RequesterId] = @RequesterId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotBefore] <= @Now
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])

    SELECT @@ROWCOUNT
END
