CREATE PROCEDURE [dbo].[OrganizationEventCleanup_UpdateError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [Attempts] = [Attempts] + 1,
        [LastError] = @Message,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
