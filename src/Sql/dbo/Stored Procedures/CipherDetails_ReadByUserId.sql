CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT DISTINCT
        C.*
    FROM
        [dbo].[CipherDetails](@UserId) C
    LEFT JOIN
        [dbo].[SubvaultCipher] SC ON SC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    WHERE
        (C.[UserId] IS NOT NULL AND C.[UserId] = @UserId)
        OR (OU.[UserId] = @UserId AND OU.[Status] = 2) -- 2 = Confirmed
END