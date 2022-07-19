CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats INT,
    @UseTotp BIT,
    @UsersGetPremium BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @MaxAutoscaleSeats INT
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
        [UseTotp] = @UseTotp,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [RevisionDate] = GETUTCDATE()
    WHERE Id = @OrganizationId
END
