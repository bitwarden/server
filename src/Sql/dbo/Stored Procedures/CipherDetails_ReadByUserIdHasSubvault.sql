CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasSubvault]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[SubvaultCipher] SC ON C.[UserId] IS NULL AND OU.[AccessAllSubvaults] = 0 AND SC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId] AND SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (OU.[AccessAllSubvaults] = 1 OR SU.[SubvaultId] IS NOT NULL)
END