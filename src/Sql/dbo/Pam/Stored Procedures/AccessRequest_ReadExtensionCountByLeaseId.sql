CREATE PROCEDURE [dbo].[AccessRequest_ReadExtensionCountByLeaseId]
    @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Includes denied extension requests. The cap itself is enforced in [AccessRequest_CreateApprovedExtension],
    -- which counts under the lease lock.
    SELECT COUNT(*)
    FROM [dbo].[AccessRequest]
    WHERE [ExtensionOfLeaseId] = @LeaseId
END
