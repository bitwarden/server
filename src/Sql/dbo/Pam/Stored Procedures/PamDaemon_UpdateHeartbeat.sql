CREATE PROCEDURE [dbo].[PamDaemon_UpdateHeartbeat]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @MinIntervalSeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Conditional bump: WHERE guard no-ops most calls, updating only after @MinIntervalSeconds.
    -- Called only by the daemon's own requests, never by a sweep.
    UPDATE [dbo].[PamDaemon]
    SET [LastHeartbeatAt] = @Now
    WHERE [Id] = @Id
        AND ([LastHeartbeatAt] IS NULL OR [LastHeartbeatAt] < DATEADD(SECOND, -@MinIntervalSeconds, @Now))
END
