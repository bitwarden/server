CREATE PROCEDURE [dbo].[OrganizationEventCleanup_MarkCompleted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [CompletedAt] = SYSUTCDATETIME(),
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
