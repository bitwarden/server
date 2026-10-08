CREATE PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_Create]
    @Id UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @Id is a plain input, not OUTPUT; caller assigns it first.
    -- Unique index backstops OneAssignmentPerConnectorTarget on a race.
    INSERT INTO [dbo].[PamAccessConnectorTargetAssignment]
    (
        [Id],
        [AccessConnectorId],
        [TargetSystemId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @AccessConnectorId,
        @TargetSystemId,
        @OrganizationId,
        @CreationDate
    )
END
