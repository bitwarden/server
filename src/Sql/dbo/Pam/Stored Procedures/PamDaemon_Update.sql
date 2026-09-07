CREATE PROCEDURE [dbo].[PamDaemon_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Name + Status only; callers use this narrow parameter set (no ApiKeyId/LastHeartbeatAt).
    UPDATE
        [dbo].[PamDaemon]
    SET
        [Name] = @Name,
        [Status] = @Status,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
