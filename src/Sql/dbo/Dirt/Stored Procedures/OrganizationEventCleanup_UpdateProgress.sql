CREATE PROCEDURE [dbo].[OrganizationEventCleanup_UpdateProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [EventsDeletedCount] = [EventsDeletedCount] + @Delta,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
