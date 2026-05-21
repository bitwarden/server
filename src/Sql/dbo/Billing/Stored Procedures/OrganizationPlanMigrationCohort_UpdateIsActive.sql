CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohort_UpdateIsActive]
    @Id UNIQUEIDENTIFIER,
    @IsActive BIT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    UPDATE [dbo].[OrganizationPlanMigrationCohort]
    SET [IsActive] = @IsActive, [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
END
