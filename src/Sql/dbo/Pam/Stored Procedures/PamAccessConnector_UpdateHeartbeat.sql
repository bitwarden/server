CREATE PROCEDURE [dbo].[PamAccessConnector_UpdateHeartbeat]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @MinIntervalSeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Conditional bump: WHERE guard no-ops most calls, updating only after @MinIntervalSeconds.
    -- Called only by the access connector's own requests, never by a sweep.
    UPDATE [dbo].[PamAccessConnector]
    SET [LastHeartbeatAt] = @Now
    WHERE [Id] = @Id
        AND ([LastHeartbeatAt] IS NULL OR [LastHeartbeatAt] < DATEADD(SECOND, -@MinIntervalSeconds, @Now))
END
