SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]
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
        [SponsoringOrganizationUserId] = @SponsoringOrganizationUserId
    and [IsAdminInitiated] = @IsAdminInitiated
END
GO
