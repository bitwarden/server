CREATE PROCEDURE [dbo].[SubvaultCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[SubvaultCipher] SC
    INNER JOIN
        [dbo].[Subvault] S ON S.[Id] = SC.[SubvaultId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON OU.[AccessAllSubvaults] = 0 AND SU.[SubvaultId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND (OU.[AccessAllSubvaults] = 1 OR SU.[SubvaultId] IS NOT NULL)
END