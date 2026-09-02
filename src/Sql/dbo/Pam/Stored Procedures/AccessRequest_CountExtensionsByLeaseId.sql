CREATE PROCEDURE [dbo].[AccessRequest_CountExtensionsByLeaseId]
    @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Number of extension requests recorded against the lease. A lease may be extended once, so this is 0 or 1; the
    -- cap itself is enforced in [AccessRequest_CreateApprovedExtension], which counts under the lease lock.
    SELECT COUNT(*)
    FROM [dbo].[AccessRequest]
    WHERE [ExtensionOfLeaseId] = @LeaseId
END
