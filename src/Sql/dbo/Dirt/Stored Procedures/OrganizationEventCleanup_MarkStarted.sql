CREATE PROCEDURE [dbo].[OrganizationEventCleanup_MarkStarted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [StartedAt] = COALESCE([StartedAt], SYSUTCDATETIME()),
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
