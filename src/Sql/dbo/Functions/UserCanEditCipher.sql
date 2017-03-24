CREATE FUNCTION [dbo].[UserCanEditCipher](@UserId UNIQUEIDENTIFIER, @CipherId UNIQUEIDENTIFIER)
RETURNS BIT AS
BEGIN
    DECLARE @CanEdit BIT

    ;WITH [CTE] AS(
        SELECT
            CASE
                WHEN OU.[Type] = 2 AND SU.[Admin] = 1 THEN 1 -- 2 = Regular User
                WHEN SU.[ReadOnly] = 0 THEN 1
                ELSE 0
            END [CanEdit]
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
        @CanEdit = CASE WHEN COUNT(1) > 0 THEN 1 ELSE 0 END
    FROM
        [CTE]
    WHERE
        [CanEdit] = 1

    RETURN @CanEdit
END
