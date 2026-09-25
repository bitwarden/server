CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignment]
    WHERE
        [Id] = @Id
END
