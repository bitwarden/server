CREATE PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [ToDelete],
        [LastSyncDate],
        [ValidUntil]
    )
    VALUES
    (
        @Id,
        @SponsoringOrganizationId,
        @SponsoringOrganizationUserID,
        @SponsoredOrganizationId,
        @FriendlyName,
        @OfferedToEmail,
        @PlanSponsorshipType,
        @ToDelete,
        @LastSyncDate,
        @ValidUntil
    )
END
GO
