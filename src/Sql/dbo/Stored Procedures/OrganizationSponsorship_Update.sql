CREATE PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @TimesRenewedWithoutValidation TINYINT,
    @SponsorshipLapsedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = @SponsoredOrganizationId,
        [TimesRenewedWithoutValidation] = @TimesRenewedWithoutValidation,
        [SponsorshipLapsedDate] = @SponsorshipLapsedDate
    WHERE
        [Id] = @Id
END
GO
