CREATE PROCEDURE [dbo].[OrganizationSecretsManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @UserSeats INT,
    @ServiceAccountSeats INT,
    @UseEnvironments BIT,
    @MaxAutoscaleUserSeats INT,
    @MaxAutoScaleServiceAccounts INT,
    @MaxProjects INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSecretsManager]
    (
        [Id],
        [OrganizationId],
        [Plan],
        [PlanType],
        [UserSeats],
        [ServiceAccountSeats],
        [UseEnvironments],
        [MaxAutoscaleUserSeats],
        [MaxAutoScaleServiceAccounts],
        [MaxProjects],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Plan,
        @PlanType,
        @UserSeats,
        @ServiceAccountSeats,
        @UseEnvironments,
        @MaxAutoscaleUserSeats,
        @MaxAutoScaleServiceAccounts,
        @MaxProjects,
        GETUTCDATE()
    )
END
