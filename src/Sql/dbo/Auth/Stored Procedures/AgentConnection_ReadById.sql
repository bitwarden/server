CREATE PROCEDURE [dbo].[AgentConnection_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AgentConnectionView]
    WHERE
        [Id] = @Id
END
