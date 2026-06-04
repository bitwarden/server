CREATE PROCEDURE [dbo].[LeaseRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[LeaseRequest]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Status] = 0 -- Pending
    ORDER BY
        [CreationDate] DESC
END
