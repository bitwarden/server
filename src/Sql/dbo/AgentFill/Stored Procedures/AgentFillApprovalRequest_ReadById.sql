CREATE PROCEDURE [dbo].[AgentFillApprovalRequest_ReadById]
    @Id     UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AgentFillApprovalRequest]
    WHERE
        [Id] = @Id
        AND [UserId] = @UserId
END
