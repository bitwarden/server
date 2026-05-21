CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadByName]
    @Name NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortView]
    WHERE
        [Name] = @Name
END
