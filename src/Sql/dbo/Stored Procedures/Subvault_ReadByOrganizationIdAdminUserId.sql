CREATE PROCEDURE [dbo].[Subvault_ReadByOrganizationIdAdminUserId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SubvaultView] S
    INNER JOIN
        [OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId]
    WHERE
        S.[OrganizationId] = @OrganizationId
        AND OU.[UserId] = @UserId
        AND OU.[Type] <= 1 -- Owner and admin
END