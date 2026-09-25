CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadMany]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortView]
    ORDER BY
        [Name] ASC
END
