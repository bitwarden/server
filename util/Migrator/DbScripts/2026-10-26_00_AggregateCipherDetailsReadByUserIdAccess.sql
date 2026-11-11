CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [OrganizationUsers] AS (
        SELECT
            OU.[Id],
            OU.[OrganizationId],
            CASE WHEN O.[UseTotp] = 1 THEN 1 ELSE 0 END [OrganizationUseTotp]
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId] AND O.[Enabled] = 1
        WHERE
            OU.[UserId] = @UserId
            AND OU.[Status] = 2 -- Confirmed
    ),
    [CollectionAccess] AS (
        SELECT
            OU.[Id] [OrganizationUserId],
            CU.[CollectionId],
            CU.[ReadOnly],
            CU.[HidePasswords],
            CU.[Manage]
        FROM
            [OrganizationUsers] OU
        INNER JOIN
            [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id]

        UNION ALL

        -- A direct grant on a collection takes precedence over the user's group grants on it
        SELECT
            OU.[Id],
            CG.[CollectionId],
            CG.[ReadOnly],
            CG.[HidePasswords],
            CG.[Manage]
        FROM
            [OrganizationUsers] OU
        INNER JOIN
            [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
        INNER JOIN
            [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId]
        WHERE
            NOT EXISTS (
                SELECT 1
                FROM [dbo].[CollectionUser] CU
                WHERE CU.[CollectionId] = CG.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
            )
    ),
    [CollectionPermissions] AS (
        -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
        SELECT
            [OrganizationUserId],
            [CollectionId],
            MAX(
                CASE WHEN [Manage] = 1 THEN 4 ELSE 0 END
                + CASE WHEN [ReadOnly] = 0 THEN 2 ELSE 0 END
                + CASE WHEN [HidePasswords] = 0 THEN 1 ELSE 0 END
            ) [Rank]
        FROM
            [CollectionAccess]
        GROUP BY
            [OrganizationUserId],
            [CollectionId]
    ),
    [CipherAccess] AS (
        SELECT
            CC.[CipherId],
            CP.[OrganizationUserId],
            MAX(CP.[Rank]) [Rank]
        FROM
            [CollectionPermissions] CP
        INNER JOIN
            [dbo].[CollectionCipher] CC ON CC.[CollectionId] = CP.[CollectionId]
        GROUP BY
            CC.[CipherId],
            CP.[OrganizationUserId]
    )
    SELECT
        C.*,
        (CA.[Rank] / 2) % 2 [Edit],
        CA.[Rank] % 2 [ViewPassword],
        CA.[Rank] / 4 [Manage],
        OU.[OrganizationUseTotp]
    FROM
        [CipherAccess] CA
    INNER JOIN
        [OrganizationUsers] OU ON OU.[Id] = CA.[OrganizationUserId]
    INNER JOIN
        [dbo].[CipherDetails](@UserId) C ON C.[Id] = CA.[CipherId] AND C.[OrganizationId] = OU.[OrganizationId] AND C.[UserId] IS NULL

    UNION ALL

    SELECT
        *,
        1 [Edit],
        1 [ViewPassword],
        1 [Manage],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    WHERE
        [UserId] = @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[CollectionCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [UserCollections] AS (
        SELECT
            CU.[CollectionId]
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id]
        INNER JOIN
            [dbo].[Collection] S ON S.[Id] = CU.[CollectionId] AND S.[OrganizationId] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND OU.[Status] = 2

        UNION

        SELECT
            CG.[CollectionId]
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
        INNER JOIN
            [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId]
        INNER JOIN
            [dbo].[Collection] S ON S.[Id] = CG.[CollectionId] AND S.[OrganizationId] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND OU.[Status] = 2
    )
    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [UserCollections] UC ON UC.[CollectionId] = CC.[CollectionId]
END
GO
