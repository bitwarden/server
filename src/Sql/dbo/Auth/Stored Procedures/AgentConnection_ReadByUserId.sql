CREATE PROCEDURE [dbo].[AgentConnection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AgentConnectionView]
    WHERE
        [UserId] = @UserId
END
