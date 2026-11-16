CREATE PROCEDURE [dbo].[AgentConnection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT TOP 1
        @UserId = [UserId]
    FROM
        [dbo].[AgentConnection]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[AgentConnection]
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
