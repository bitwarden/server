CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledAt DATETIME2 (7),
    @MigratedAt DATETIME2 (7),
    @ChurnDiscountAppliedAt DATETIME2 (7),
    @CreatedAt DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    -- @OrganizationId, @CohortId, and @CreatedAt are accepted but not assigned; they are
    -- immutable once the row is inserted. The assignment-to-cohort and assignment-to-org
    -- relationships cannot be changed after creation -- create a new row instead.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    SET
        [ScheduledAt] = @ScheduledAt,
        [MigratedAt] = @MigratedAt,
        [ChurnDiscountAppliedAt] = @ChurnDiscountAppliedAt,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
