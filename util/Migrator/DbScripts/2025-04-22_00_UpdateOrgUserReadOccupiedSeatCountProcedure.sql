-- Update OrganizationUser_ReadOccupiedSeatCountByOrganizationId to include admin-initiated sponsorships
-- Based on https://bitwarden.atlassian.net/browse/PM-17772
IF OBJECT_ID('[dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadOccupiedSeatCountByOrganizationId]
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
        ) + 
        (
            -- Count admin-initiated sponsorships towards the seat count
            -- Introduced in https://bitwarden.atlassian.net/browse/PM-17772
            SELECT COUNT(1)
            FROM [dbo].[OrganizationSponsorship]
            WHERE SponsoringOrganizationId = @OrganizationId
            AND IsAdminInitiated = 1
        )
END
GO 