CREATE PROCEDURE [dbo].[OrganizationEventCleanup_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @QueuedAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationEventCleanup]
    (
        [Id],
        [OrganizationId],
        [QueuedAt]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @QueuedAt
    )
END
