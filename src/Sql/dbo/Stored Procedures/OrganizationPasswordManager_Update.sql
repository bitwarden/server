CREATE PROCEDURE [dbo].[OrganizationPasswordManager_Update]
    @OrganizationId UNIQUEIDENTIFIER,
    @Plan [nvarchar](50),
    @PlanType [tinyint],
    @Seats [int],
    @MaxCollections [smallint],
    @UseTotp [bit],
    @UsersGetPremium [bit],
    @Storage [bigint],
    @MaxStorageGb [smallint],
    @MaxAutoscaleSeats [int]
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPasswordManager]
    SET
        [Plan] = @Plan,
        PlanType = @PlanType,
        Seats = @Seats,
        MaxCollections = @MaxCollections,
        UseTotp = @UseTotp,
        UsersGetPremium = @UsersGetPremium,
        Storage = @Storage,
        MaxStorageGb = @MaxStorageGb,
        MaxAutoscaleSeats = @MaxAutoscaleSeats,
        RevisionDate = GETUTCDATE()
    WHERE
         [OrganizationId] = @OrganizationId
END