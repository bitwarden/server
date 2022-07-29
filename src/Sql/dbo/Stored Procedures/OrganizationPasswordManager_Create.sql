CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50) = NULL,
    @PlanType TINYINT = NULL,
    @Seats INT = NULL,
    @MaxCollections SMALLINT = NULL,
    @UseTotp BIT = NULL,
    @UsersGetPremium BIT = NULL,
    @Storage BIGINT = NULL,
    @MaxStorageGb SMALLINT = NULL,
    @MaxAutoscaleSeats INT = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPasswordManager]
    (
        [Id],
        [OrganizationId],
        [Plan],
        [PlanType],
        [Seats],
        [MaxCollections],
        [UseTotp],
        [UsersGetPremium],
        [Storage],
        [MaxStorageGb],
        [MaxAutoscaleSeats],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Plan,
        @PlanType,
        @Seats,
        @MaxCollections,
        @UseTotp,
        @UsersGetPremium,
        @Storage,
        @MaxStorageGb,
        @MaxAutoscaleSeats,
        GETUTCDATE()
    )

    UPDATE [dbo].[Organization]
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
    WHERE Id = @OrganizationId
END

