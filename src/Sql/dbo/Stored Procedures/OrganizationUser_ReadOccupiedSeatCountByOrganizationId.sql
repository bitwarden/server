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
