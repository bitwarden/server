CREATE PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @CloudSponsor BIT,
    @LastSyncDate DATETIME2 (7),
    @TimesRenewedWithoutValidation TINYINT,
    @SponsorshipLapsedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [InstallationId] = @InstallationId,
        [SponsoringOrganizationId] = @SponsoringOrganizationId,
        [SponsoringOrganizationUserID] = @SponsoringOrganizationUserID,
        [SponsoredOrganizationId] = @SponsoredOrganizationId,
        [OfferedToEmail] = @OfferedToEmail,
        [PlanSponsorshipType] = @PlanSponsorshipType,
        [CloudSponsor] = @CloudSponsor,
        [LastSyncDate] = @LastSyncDate,
        [TimesRenewedWithoutValidation] = @TimesRenewedWithoutValidation,
        [SponsorshipLapsedDate] = @SponsorshipLapsedDate
    WHERE
        [Id] = @Id
END
GO
