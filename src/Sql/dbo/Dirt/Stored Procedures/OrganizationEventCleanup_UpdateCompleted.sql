CREATE PROCEDURE [dbo].[OrganizationEventCleanup_UpdateCompleted]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [CompletedDate] = @Now,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
