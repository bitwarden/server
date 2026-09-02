CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadManyByNames]
    @JsonData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
    FROM
        [dbo].[OrganizationPlanMigrationCohortView] C
    INNER JOIN
        OPENJSON(@JsonData) WITH ([Name] NVARCHAR(255) '$.Name') N ON C.[Name] = N.[Name]
END
