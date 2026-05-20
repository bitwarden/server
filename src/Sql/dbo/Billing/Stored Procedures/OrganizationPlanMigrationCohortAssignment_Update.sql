CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CohortId UNIQUEIDENTIFIER,
    @ScheduledDate DATETIME2(7),
    @MigratedDate DATETIME2(7),
    @ChurnDiscountAppliedDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @OrganizationId, @CohortId, and @CreationDate are accepted but not assigned; they are
    -- immutable once the row is inserted. The assignment-to-cohort and assignment-to-org
    -- relationships cannot be changed after creation -- create a new row instead.
    UPDATE
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    SET
        [ScheduledDate] = @ScheduledDate,
        [MigratedDate] = @MigratedDate,
        [ChurnDiscountAppliedDate] = @ChurnDiscountAppliedDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
