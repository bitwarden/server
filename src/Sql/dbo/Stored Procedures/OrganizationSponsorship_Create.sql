CREATE PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [InstallationId],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [CloudSponsor],
        [LastSyncDate],
        [TimesRenewedWithoutValidation],
        [SponsorshipLapsedDate]
    )
    VALUES
    (
        @Id,
        @InstallationId,
        @SponsoringOrganizationId,
        @SponsoringOrganizationUserID,
        @SponsoredOrganizationId,
        @OfferedToEmail,
        @PlanSponsorshipType,
        @CloudSponsor,
        @LastSyncDate,
        @TimesRenewedWithoutValidation,
        @SponsorshipLapsedDate
    )
END
GO
