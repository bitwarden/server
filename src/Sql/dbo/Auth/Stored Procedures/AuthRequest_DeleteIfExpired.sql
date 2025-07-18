-- UserExpirationSeconds to 15 minutes (15 * 60)
-- AdminExpirationSeconds to 7 days (7 * 24 * 60 * 60)
-- AdminApprovalExpirationSeconds to 12 hour (12 * 60 * 60)

CREATE PROCEDURE [dbo].[AuthRequest_DeleteIfExpired]
    @UserExpirationSeconds INT = 900,
    @AdminExpirationSeconds INT = 604800,
    @AdminApprovalExpirationSeconds INT = 43200
AS
BEGIN
    SET NOCOUNT OFF
    DELETE FROM [dbo].[AuthRequest]
        -- User requests expire after 15 minutes (by default) of their creation
    WHERE ([Type] != 2 AND DATEADD(second, @UserExpirationSeconds, [CreationDate]) < GETUTCDATE())
        -- Admin requests expire after 7 days (by default) of their creation if they have not been approved
        OR ([Type] = 2 AND ([Approved] IS NULL OR [Approved] = 0) AND DATEADD(second, @AdminExpirationSeconds,[CreationDate]) < GETUTCDATE())
        -- Admin requests expire after 12 hours (by default) of their approval
        OR ([Type] = 2 AND [Approved] = 1 AND DATEADD(second, @AdminApprovalExpirationSeconds, [ResponseDate]) < GETUTCDATE());
END
