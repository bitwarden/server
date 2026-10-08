CREATE PROCEDURE [dbo].[AgentFillApprovalRequest_DeleteExpired]
    @ExpiredBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[AgentFillApprovalRequest]
    WHERE
        [ExpirationDate] < @ExpiredBefore

    SELECT @@ROWCOUNT
END
