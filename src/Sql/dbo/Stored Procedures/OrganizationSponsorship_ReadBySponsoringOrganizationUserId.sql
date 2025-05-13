CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]
    @SponsoringOrganizationUserId UNIQUEIDENTIFIER,
    @IsAdminInitiated BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationUserID] = @SponsoringOrganizationUserId
    and [IsAdminInitiated] = @IsAdminInitiated
END
