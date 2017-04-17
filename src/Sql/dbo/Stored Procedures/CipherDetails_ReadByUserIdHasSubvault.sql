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
        [dbo].[SubvaultCipher] SC ON C.[UserId] IS NULL AND SC.[CipherId] = C.[Id]
    INNER JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    INNER JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId 
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
END