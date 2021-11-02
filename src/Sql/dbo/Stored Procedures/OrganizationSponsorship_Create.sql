CREATE PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @OfferedToEmail NVARCHAR(256) -- Should not be null
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [CloudSponsor],
        [OfferedToEmail]
    )
    VALUES
    (
        @Id,
        @SponsoringOrganizationId,
        @SponsoringOrganizationUserID,
        1, -- Should only be called by cloud sponsorship
        @OfferedToEmail
    )
END
GO
