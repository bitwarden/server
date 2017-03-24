CREATE PROCEDURE [dbo].[SubvaultUser_ReadIsAdminByCipherIdUserId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [CTE] AS(
        SELECT
            CASE WHEN OU.[Type] = 2 THEN SU.[Admin] ELSE 1 END AS [Admin] -- 2 = Regular User
        FROM
            [dbo].[SubvaultUser] SU
        INNER JOIN
            [dbo].[SubvaultCipher] SC ON SC.SubvaultId = SU.SubvaultId
        INNER JOIN
            [dbo].[Cipher] C ON SC.[CipherId] = C.[Id]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.Id = SU.OrganizationUserId AND OU.OrganizationId = C.OrganizationId
        WHERE
            C.[Id] = @CipherId
            AND OU.[UserId] = @UserId
            AND OU.[Status] = 2 -- 2 = Confirmed
    )
    SELECT
        CASE WHEN COUNT(1) > 0 THEN 1 ELSE 0 END
    FROM
        [CTE]
    WHERE
        [Admin] = 1
END