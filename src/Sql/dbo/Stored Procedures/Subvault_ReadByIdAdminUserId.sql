CREATE PROCEDURE [dbo].[Subvault_ReadByIdAdminUserId]
    @Id UNIQUEIDENTIFIER,
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
        S.[Id] = @Id
        AND OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- Confirmed
        AND OU.[Type] <= 1 -- Owner and admin
END