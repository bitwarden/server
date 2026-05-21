CREATE OR ALTER PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_CountNonPendingByCohortId]
    @CohortId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT COUNT(1)
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignment] A
        INNER JOIN [dbo].[OrganizationPlanMigrationCohort] C ON C.[Id] = A.[CohortId]
    WHERE
        A.[CohortId] = @CohortId
        AND (
            (C.[MigrationPathId] IS NOT NULL AND A.[ScheduledDate] IS NOT NULL)
            OR
            (C.[MigrationPathId] IS NULL AND A.[ChurnDiscountAppliedDate] IS NOT NULL)
        )
END
GO
