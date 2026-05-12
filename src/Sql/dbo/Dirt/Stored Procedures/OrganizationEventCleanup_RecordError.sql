CREATE PROCEDURE [dbo].[OrganizationEventCleanup_RecordError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationEventCleanup]
    SET
        [Attempts] = [Attempts] + 1,
        [LastError] = @Message,
        [LastProgressAt] = SYSUTCDATETIME()
    WHERE
        [Id] = @Id
END
