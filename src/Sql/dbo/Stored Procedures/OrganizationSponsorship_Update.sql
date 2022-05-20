CREATE PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @ToDelete BIT,
    @LastSyncDate DATETIME2 (7),
    @ValidUntil DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoringOrganizationId] = @SponsoringOrganizationId,
        [SponsoringOrganizationUserID] = @SponsoringOrganizationUserID,
        [SponsoredOrganizationId] = @SponsoredOrganizationId,
        [FriendlyName] = @FriendlyName,
        [OfferedToEmail] = @OfferedToEmail,
        [PlanSponsorshipType] = @PlanSponsorshipType,
        [ToDelete] = @ToDelete,
        [LastSyncDate] = @LastSyncDate,
        [ValidUntil] = @ValidUntil
    WHERE
        [Id] = @Id
END
GO
