CREATE PROCEDURE [dbo].[AccessLease_ReadByAccessRequestId]
    @AccessRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A request produces at most one lease, enforced by the unique [IX_AccessLease_AccessRequestId].
    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [AccessRequestId] = @AccessRequestId
END
