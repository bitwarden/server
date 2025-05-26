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
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @OrganizationId
        AND Status >= 0 --Invited
END
GO
