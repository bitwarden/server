CREATE PROCEDURE [dbo].[AccessRequest_CountExtensionsByLeaseId]
    @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Number of extension requests recorded against the lease. Extensions are always auto-approved, so every such
    -- request counts toward the governing rule's per-lease maximum.
    SELECT COUNT(*)
    FROM [dbo].[AccessRequest]
    WHERE [ExtensionOfLeaseId] = @LeaseId
END
