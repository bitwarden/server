CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortView]
    WHERE
        [Id] = @Id
END
