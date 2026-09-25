CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadByName]
    @Name NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortView]
    WHERE
        [Name] = @Name
END
