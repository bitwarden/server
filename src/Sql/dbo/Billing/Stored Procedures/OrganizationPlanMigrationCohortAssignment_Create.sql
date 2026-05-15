CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationPlanMigrationCohortAssignment]
    (
        [Id],
        [OrganizationId],
        [CohortId],
        [ScheduledAt],
        [MigratedAt],
        [ChurnDiscountAppliedAt],
        [CreatedAt],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CohortId,
        @ScheduledAt,
        @MigratedAt,
        @ChurnDiscountAppliedAt,
        @CreatedAt,
        @RevisionDate
    )
END
