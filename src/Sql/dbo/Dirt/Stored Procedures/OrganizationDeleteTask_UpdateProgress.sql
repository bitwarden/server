CREATE PROCEDURE [dbo].[OrganizationDeleteTask_UpdateProgress]
    @Id UNIQUEIDENTIFIER,
    @Delta BIGINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationDeleteTask]
    SET
        [ItemsDeletedCount] = [ItemsDeletedCount] + @Delta,
        [RevisionDate] = @Now
    WHERE
        [Id] = @Id
END
