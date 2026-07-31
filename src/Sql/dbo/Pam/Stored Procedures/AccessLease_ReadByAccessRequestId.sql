CREATE PROCEDURE [dbo].[AccessLease_ReadByAccessRequestId]
    @AccessRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique); TOP 1 is belt and braces.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [AccessRequestId] = @AccessRequestId
    ORDER BY
        [CreationDate] DESC
END
