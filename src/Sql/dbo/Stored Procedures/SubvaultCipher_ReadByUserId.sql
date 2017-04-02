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
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- Confirmed
END
