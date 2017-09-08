CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Type] = 0
        AND OU.[Status] = 2 -- 2 = Confirmed
END