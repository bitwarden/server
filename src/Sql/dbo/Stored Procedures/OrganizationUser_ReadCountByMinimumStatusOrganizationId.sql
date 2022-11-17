CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByMinimumStatusOrganizationId]
    @Id UNIQUEIDENTIFIER
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @Id
        AND Status >= @MinimumStatus
END