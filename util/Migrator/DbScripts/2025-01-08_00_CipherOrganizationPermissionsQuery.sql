CREATE OR ALTER PROCEDURE [dbo].[CipherOrganizationPermissions_GetManyByOrganizationId]
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
    UserPermissions AS (
        SELECT DISTINCT
            CC.[CipherId],
            CASE WHEN CC.[CollectionId] IS NULL THEN 0 ELSE 1 END as [Read],
            CASE WHEN CU.[HidePasswords] = 0 THEN 1 ELSE 0 END as [ViewPassword],
            CASE WHEN CU.[ReadOnly] = 0 THEN 1 ELSE 0 END as [Edit],
            COALESCE(CU.[Manage], 0) as [Manage]
        FROM [dbo].[CollectionCipher] CC
        INNER JOIN [dbo].[CollectionUser] CU ON
            CU.[CollectionId] = CC.[CollectionId]
            AND CU.[OrganizationUserId] = (
                SELECT [Id] FROM [OrganizationUser]
                WHERE [UserId] = @UserId
                AND [OrganizationId] = @OrganizationId
            )
    ),
    GroupPermissions AS (
        SELECT DISTINCT
            CC.[CipherId],
            CASE WHEN CC.[CollectionId] IS NULL THEN 0 ELSE 1 END as [Read],
            CASE WHEN CG.[HidePasswords] = 0 THEN 1 ELSE 0 END as [ViewPassword],
            CASE WHEN CG.[ReadOnly] = 0 THEN 1 ELSE 0 END as [Edit],
            COALESCE(CG.[Manage], 0) as [Manage]
        FROM [dbo].[CollectionCipher] CC
        INNER JOIN [dbo].[CollectionGroup] CG ON
            CG.[CollectionId] = CC.[CollectionId]
        INNER JOIN [dbo].[GroupUser] GU ON
            GU.[GroupId] = CG.[GroupId]
            AND GU.[OrganizationUserId] = (
                SELECT [Id] FROM [OrganizationUser]
                WHERE [UserId] = @UserId
                AND [OrganizationId] = @OrganizationId
            )
        WHERE NOT EXISTS (
            SELECT 1
            FROM UserPermissions UP
            WHERE UP.[CipherId] = CC.[CipherId]
        )
    ),
    CombinedPermissions AS (
        SELECT CipherId, [Read], ViewPassword, Edit, Manage
        FROM UserPermissions
        UNION ALL
        SELECT CipherId, [Read], ViewPassword, Edit, Manage
        FROM GroupPermissions
    )
    SELECT
        C.[Id],
        C.[OrganizationId],
        ISNULL(MAX(P.[Read]), 0) as [Read],
        ISNULL(MAX(P.[ViewPassword]), 0) as [ViewPassword],
        ISNULL(MAX(P.[Edit]), 0) as [Edit],
        ISNULL(MAX(P.[Manage]), 0) as [Manage]
    FROM BaseCiphers C
    LEFT JOIN CombinedPermissions P ON P.CipherId = C.[Id]
    GROUP BY C.[Id], C.[OrganizationId]
END
GO
