CREATE PROCEDURE [dbo].[AccessLease_ExpireDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The anticipated lease natural-expiry sweep (plan decision 4): flips Active -> Expired for leases whose window
    -- closed on its own (no revoke/cancel involved), so the deferred LeaseExpired audit kind and the rotation
    -- access-end trigger both have something to fire from. [IX_AccessLease_NotAfter_Status] makes this a narrow
    -- range seek. No join is needed for the projection -- every column the caller audits/triggers on already lives
    -- on the row itself.
    UPDATE [dbo].[AccessLease]
    SET [Status] = 1 -- Expired
    OUTPUT
        deleted.[Id],
        deleted.[OrganizationId],
        deleted.[CollectionId],
        deleted.[CipherId],
        deleted.[RequesterId],
        deleted.[NotBefore],
        deleted.[NotAfter]
    WHERE [Status] = 0 -- Active
        AND [NotAfter] <= @Now
END
