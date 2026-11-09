CREATE PROCEDURE [dbo].[CipherOrganizationPermissions_GetManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH BaseCiphers AS (
        SELECT C.[Id], C.[OrganizationId]
        FROM [dbo].[CipherDetails](@UserId) C
        INNER JOIN [OrganizationUser] OU ON
            C.[UserId] IS NULL
            AND C.[OrganizationId] = @OrganizationId
            AND OU.[UserId] = @UserId
        INNER JOIN [dbo].[Organization] O ON
            O.[Id] = OU.[OrganizationId]
            AND O.[Id] = C.[OrganizationId]
            AND O.[Enabled] = 1
    ),
    CollectionAccess AS (
        SELECT
            CU.[CollectionId],
            CU.[ReadOnly],
            CU.[HidePasswords],
            CU.[Manage]
        FROM [dbo].[CollectionUser] CU
        WHERE CU.[OrganizationUserId] = (
            SELECT [Id] FROM [OrganizationUser]
            WHERE [UserId] = @UserId
            AND [OrganizationId] = @OrganizationId
        )

        UNION ALL

        -- A direct grant on a collection takes precedence over the user's group grants on it
        SELECT
            CG.[CollectionId],
            CG.[ReadOnly],
            CG.[HidePasswords],
            CG.[Manage]
        FROM [dbo].[CollectionGroup] CG
        INNER JOIN [dbo].[GroupUser] GU ON
            GU.[GroupId] = CG.[GroupId]
            AND GU.[OrganizationUserId] = (
                SELECT [Id] FROM [OrganizationUser]
                WHERE [UserId] = @UserId
                AND [OrganizationId] = @OrganizationId
            )
        WHERE NOT EXISTS (
            SELECT 1
            FROM [dbo].[CollectionUser] CU
            WHERE CU.[CollectionId] = CG.[CollectionId]
            AND CU.[OrganizationUserId] = GU.[OrganizationUserId]
        )
    ),
    CollectionPermissions AS (
        -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
        SELECT
            [CollectionId],
            MAX(
                CASE WHEN [Manage] = 1 THEN 4 ELSE 0 END
                + CASE WHEN [ReadOnly] = 0 THEN 2 ELSE 0 END
                + CASE WHEN [HidePasswords] = 0 THEN 1 ELSE 0 END
            ) [Rank]
        FROM CollectionAccess
        GROUP BY [CollectionId]
    ),
    CipherPermissions AS (
        SELECT
            CC.[CipherId],
            MAX(CP.[Rank]) [Rank]
        FROM CollectionPermissions CP
        INNER JOIN [dbo].[CollectionCipher] CC ON CC.[CollectionId] = CP.[CollectionId]
        GROUP BY CC.[CipherId]
    )
    SELECT
        C.[Id],
        C.[OrganizationId],
        -- Read is any rank; ViewPassword (1), Edit (2) and Manage (4) are the parts of the highest rank
        CASE WHEN P.[Rank] IS NULL THEN 0 ELSE 1 END as [Read],
        ISNULL(P.[Rank] % 2, 0) as [ViewPassword],
        ISNULL((P.[Rank] / 2) % 2, 0) as [Edit],
        ISNULL(P.[Rank] / 4, 0) as [Manage]
    FROM BaseCiphers C
    LEFT JOIN CipherPermissions P ON P.CipherId = C.[Id]
END
