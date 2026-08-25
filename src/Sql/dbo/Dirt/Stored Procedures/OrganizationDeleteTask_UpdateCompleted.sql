CREATE PROCEDURE [dbo].[OrganizationDeleteTask_UpdateCompleted]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [CompletedDate] = @Now,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
