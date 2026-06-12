CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationPlanMigrationCohortAssignment]
    (
        [Id],
        [OrganizationId],
        [CohortId],
        [ScheduledDate],
        [MigratedDate],
        [ChurnDiscountAppliedDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CohortId,
        @ScheduledDate,
        @MigratedDate,
        @ChurnDiscountAppliedDate,
        @CreationDate,
        @RevisionDate
    )
END
