CREATE FUNCTION [dbo].[UserCanEditCipher](@UserId UNIQUEIDENTIFIER, @CipherId UNIQUEIDENTIFIER)
RETURNS BIT AS
BEGIN
    DECLARE @CanEdit BIT

    ;WITH [CTE] AS(
        SELECT
            CASE WHEN OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 THEN 1 ELSE 0 END [CanEdit]
        FROM
            [dbo].[Cipher] C
        INNER JOIN
            [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
        LEFT JOIN
            [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
        WHERE
            C.[Id] = @CipherId
            AND OU.[Status] = 2 -- 2 = Confirmed
            AND O.[Enabled] = 1
            AND (OU.[AccessAll] = 1 OR CU.[CollectionId] IS NOT NULL)
    )
    SELECT
        @CanEdit = CASE WHEN COUNT(1) > 0 THEN 1 ELSE 0 END
    FROM
        [CTE]
    WHERE
        [CanEdit] = 1

    RETURN @CanEdit
END