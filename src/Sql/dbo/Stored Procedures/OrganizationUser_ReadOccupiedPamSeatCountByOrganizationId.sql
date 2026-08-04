CREATE PROCEDURE [dbo].[OrganizationUser_ReadOccupiedPamSeatCountByOrganizationId]
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
        AND Status IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND AccessPam = 1
END
GO
