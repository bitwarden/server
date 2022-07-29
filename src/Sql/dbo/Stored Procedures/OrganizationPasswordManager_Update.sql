CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Update]
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan [nvarchar](50) = NULL,
    @PlanType [tinyint] = NULL,
    @Seats [int] = NULL,
    @MaxCollections [smallint] = NULL,
    @UseTotp [bit] = NULL,
    @UsersGetPremium [bit] = NULL,
    @Storage [bigint] = NULL,
    @MaxStorageGb [smallint] = NULL,
    @MaxAutoscaleSeats [int] = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPasswordManager]
    SET
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UseTotp] = @UseTotp,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [OrganizationId] = @OrganizationId

    UPDATE
        [dbo].[Organization]
    SET
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UseTotp] = @UseTotp,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @OrganizationId
END
