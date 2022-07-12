CREATE PROCEDURE [dbo].[OrganizationSecretManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @UserSeats INT,
    @ServiceAccountSeats INT,
    @UseEnvironments BIT,
    @NaxAutoscaleUserSeats INT,
    @MaxAutoScaleServiceAccounts INT,
    @MaxProjects INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSecretManager]
    (
        [Id],
        [OrganizationId],
        [Plan],
        [PlanType],
        [UserSeats],
        [ServiceAccountSeats],
        [UseEnvironments],
        [NaxAutoscaleUserSeats],
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
        @NaxAutoscaleUserSeats,
        @MaxAutoScaleServiceAccounts,
        @MaxProjects,
        GETUTCDATE()
    )
END