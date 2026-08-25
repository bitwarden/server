CREATE PROCEDURE [dbo].[OrganizationDeleteTask_UpdateError]
    @Id UNIQUEIDENTIFIER,
    @Message NVARCHAR(MAX),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [FailureCount] = [FailureCount] + 1,
        [LastError] = @Message,
        [RevisionDate] = @Now
    OUTPUT
        inserted.[FailureCount]
    WHERE
        [Id] = @Id
END
