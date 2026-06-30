CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignmentView]
    WHERE
        [OrganizationId] = @OrganizationId
END
