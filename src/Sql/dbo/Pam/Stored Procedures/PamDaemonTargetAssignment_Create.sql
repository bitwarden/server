CREATE PROCEDURE [dbo].[PamDaemonTargetAssignment_Create]
    @Id UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @Id is a plain input, not OUTPUT: unlike the generic Create sprocs, the caller (IPamDaemonRepository.
    -- CreateAssignmentAsync) always assigns the id before calling this. [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
    -- is the unique-index backstop for OneAssignmentPerDaemonTarget if two callers race.
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
