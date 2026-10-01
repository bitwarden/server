CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_Create]
    @Id UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @Id is a plain input, not OUTPUT; caller assigns it first.
    -- Unique index backstops OneAssignmentPerDaemonTarget on a race.
    INSERT INTO [dbo].[PamDaemonTargetAssignment]
    (
        [Id],
        [DaemonId],
        [TargetSystemId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @DaemonId,
        @TargetSystemId,
        @OrganizationId,
        @CreationDate
    )
END
