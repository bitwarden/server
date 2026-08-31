CREATE PROCEDURE [dbo].[PamDaemon_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Name + Status only: ApiKeyId is set once at registration (reissue is deferred -- see the plan's deferrals),
    -- OrganizationId/CreationDate never change, and LastHeartbeatAt has its own conditional-bump sproc
    -- (PamDaemon_UpdateHeartbeat) so a routine admin edit never races a daemon's own poll. The repository must call
    -- this with an explicit narrow parameter set rather than the generic whole-entity Update -- passing the full
    -- PamDaemon entity here would fail (this sproc does not declare an ApiKeyId/LastHeartbeatAt/etc. parameter).
    UPDATE
        [dbo].[PamDaemon]
    SET
        [Name] = @Name,
        [Status] = @Status,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
