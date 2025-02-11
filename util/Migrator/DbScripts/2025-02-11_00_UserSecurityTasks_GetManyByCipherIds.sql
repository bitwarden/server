CREATE OR ALTER PROCEDURE [dbo].[UserSecurityTasks_GetManyByCipherIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    ;WITH BaseCiphers AS (
        SELECT C.[Id], C.[OrganizationId]
        FROM [dbo].[Cipher] C
        INNER JOIN @CipherIds CI ON C.[Id] = CI.[Id]
        INNER JOIN [dbo].[Organization] O ON
            O.[Id] = C.[OrganizationId]
            AND O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
    ),
    UserPermissions AS (
        SELECT DISTINCT
            CC.[CipherId],
            OU.[UserId],
            COALESCE(CU.[Manage], 0) as [Manage]
        FROM [dbo].[CollectionCipher] CC
        INNER JOIN [dbo].[CollectionUser] CU ON
            CU.[CollectionId] = CC.[CollectionId]
        INNER JOIN [dbo].[OrganizationUser] OU ON
            CU.[OrganizationUserId] = OU.[Id]
            AND OU.[OrganizationId] = @OrganizationId
        WHERE COALESCE(CU.[Manage], 0) = 1
    ),
    GroupPermissions AS (
        SELECT DISTINCT
            CC.[CipherId],
            OU.[UserId],
            COALESCE(CG.[Manage], 0) as [Manage]
        FROM [dbo].[CollectionCipher] CC
        INNER JOIN [dbo].[CollectionGroup] CG ON
            CG.[CollectionId] = CC.[CollectionId]
        INNER JOIN [dbo].[GroupUser] GU ON
            GU.[GroupId] = CG.[GroupId]
        INNER JOIN [dbo].[OrganizationUser] OU ON
            GU.[OrganizationUserId] = OU.[Id]
            AND OU.[OrganizationId] = @OrganizationId
        WHERE COALESCE(CG.[Manage], 0) = 1
            AND NOT EXISTS (
                SELECT 1
                FROM UserPermissions UP
                WHERE UP.[CipherId] = CC.[CipherId]
                AND UP.[UserId] = OU.[UserId]
            )
    ),
    CombinedPermissions AS (
        SELECT CipherId, UserId, [Manage]
        FROM UserPermissions
        UNION
        SELECT CipherId, UserId, [Manage]
        FROM GroupPermissions
    )
    SELECT
        P.[UserId],
        U.[Email],
        C.[Id] as CipherId
    FROM BaseCiphers C
    INNER JOIN CombinedPermissions P ON P.CipherId = C.[Id]
    INNER JOIN [dbo].[User] U ON U.[Id] = P.[UserId]
    WHERE P.[Manage] = 1
    ORDER BY U.[Email], C.[Id]
END
GO
