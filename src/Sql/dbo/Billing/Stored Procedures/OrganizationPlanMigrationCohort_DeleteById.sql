CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPlanMigrationCohort]
    WHERE
        [Id] = @Id
END
