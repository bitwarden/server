CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadOccupiedSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT
        (
            -- Count organization users
            SELECT COUNT(1)
            FROM [dbo].[OrganizationUserView]
            WHERE OrganizationId = @OrganizationId
            AND Status >= 0 --Invited
        ) as Users,
        (
            -- Count admin-initiated sponsorships towards the seat count
            -- Introduced in https://bitwarden.atlassian.net/browse/PM-17772
            SELECT COUNT(1)
            FROM [dbo].[OrganizationSponsorship]
            WHERE SponsoringOrganizationId = @OrganizationId
            AND IsAdminInitiated = 1
            AND (
                -- Not marked for deletion - always count
                (ToDelete = 0) 
                OR
                -- Marked for deletion but has a valid until date in the future (RevokeWhenExpired status)
                (ToDelete = 1 AND ValidUntil IS NOT NULL AND ValidUntil > GETUTCDATE())
            )
            AND (
                -- SENT status: When SponsoredOrganizationId is null
                SponsoredOrganizationId IS NULL
                OR
                -- ACCEPTED status: When SponsoredOrganizationId is not null and ValidUntil is null or in the future
                (SponsoredOrganizationId IS NOT NULL AND (ValidUntil IS NULL OR ValidUntil > GETUTCDATE()))
            )
        ) as Sponsored
END
GO
