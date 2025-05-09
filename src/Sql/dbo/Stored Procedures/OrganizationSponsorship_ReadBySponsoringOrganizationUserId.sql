CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]
    @SponsoringOrganizationUserId UNIQUEIDENTIFIER,
    @IsAdminInitiated BIT = 0
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationUserId] = @SponsoringOrganizationUserId
    and [IsAdminInitiated] = @IsAdminInitiated
END 