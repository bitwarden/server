CREATE PROCEDURE [dbo].[OrganizationEventCleanup_IncrementProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [EventsDeletedCount] = [EventsDeletedCount] + @Delta,
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
