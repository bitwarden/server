CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignmentView]
    WHERE
        [Id] = @Id
END
