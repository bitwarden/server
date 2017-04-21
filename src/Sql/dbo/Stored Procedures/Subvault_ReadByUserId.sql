CREATE PROCEDURE [dbo].[Subvault_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SubvaultView] S
    INNER JOIN
        [Organization] O ON O.[Id] = S.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON OU.[AccessAllSubvaults] = 0 AND SU.[SubvaultId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND O.[Enabled] = 1
        AND (OU.[AccessAllSubvaults] = 1 OR SU.[SubvaultId] IS NOT NULL)
END