CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasSubvault]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT DISTINCT
        C.*
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [dbo].[SubvaultCipher] SC ON SC.[CipherId] = C.[Id]
    INNER JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId 
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
END