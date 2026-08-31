CREATE PROCEDURE [dbo].[PamDaemon_UpdateHeartbeat]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @MinIntervalSeconds INT
AS
BEGIN
    SET NOCOUNT ON

    -- Conditional bump: the daemon-facing request filter calls this on every request, so the WHERE guard turns
    -- most calls into a no-op write instead of hammering the row -- only a poll arriving after @MinIntervalSeconds
    -- since the last recorded heartbeat actually updates it. Never called by a sweep -- only by the daemon's own
    -- requests.
    UPDATE [dbo].[PamDaemon]
    SET [LastHeartbeatAt] = @Now
    WHERE [Id] = @Id
        AND ([LastHeartbeatAt] IS NULL OR [LastHeartbeatAt] < DATEADD(SECOND, -@MinIntervalSeconds, @Now))
END
