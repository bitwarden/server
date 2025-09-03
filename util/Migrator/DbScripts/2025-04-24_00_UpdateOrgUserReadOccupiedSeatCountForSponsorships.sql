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
            SELECT COUNT(1)
            FROM [dbo].[OrganizationUserView]
            WHERE OrganizationId = @OrganizationId
            AND Status >= 0 --Invited
        ) + 
        (
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
        )
END
GO
