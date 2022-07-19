CREATE PROCEDURE [dbo].[OrganizationSecretsManager_Update]
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan [nvarchar](50),
    @PlanType [tinyint],
    @UserSeats [int],
    @ServiceAccountSeats [int],
    @UseEnvironments [bit],
    @MaxAutoscaleUserSeats [int],
    @MaxAutoscaleServiceAccounts [int],
    @MaxProjects [int]
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSecretsManager]
    SET
        [OrganizationId] = @OrganizationId,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [UserSeats] = @UserSeats,
        [ServiceAccountSeats] = @ServiceAccountSeats,
        [UseEnvironments] = @UseEnvironments,
        [MaxAutoscaleUserSeats] = @MaxAutoscaleUserSeats,
        [MaxAutoscaleServiceAccounts] = @MaxAutoscaleServiceAccounts,
        [MaxProjects] = @MaxProjects,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [OrganizationId] = @OrganizationId
END
