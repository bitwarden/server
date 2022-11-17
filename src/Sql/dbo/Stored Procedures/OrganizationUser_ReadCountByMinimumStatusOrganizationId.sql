CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByMinimumStatusOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @OrganizationId
        AND Status >= @MinimumStatus
END