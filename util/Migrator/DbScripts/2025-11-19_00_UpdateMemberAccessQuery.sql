CREATE OR ALTER VIEW [dbo].[CollectionCipherDetailsView]
AS
SELECT
    CC.[CollectionId],
    C.[OrganizationId] AS [CollectionOrganizationId],
    CC.[CipherId],
    Ci.[OrganizationId] AS [CipherOrganizationId],
    Ci.[DeletedDate]
FROM
    [dbo].[CollectionCipher] CC
INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
INNER JOIN
    [dbo].[Cipher] Ci ON Ci.[Id] = CC.[CipherId]
GO

CREATE OR ALTER VIEW [dbo].[CollectionGroupPermissionsView]
AS
SELECT
    GU.[OrganizationUserId],
    G.[Id] AS [GroupId],
    G.[Name] AS [GroupName],
    G.[OrganizationId],
    CG.[CollectionId],
    C.[Name] AS [CollectionName],
    CG.[ReadOnly],
    CG.[HidePasswords],
    CG.[Manage]
FROM
    [dbo].[GroupUser] GU
INNER JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
INNER JOIN
    [dbo].[CollectionGroup] CG ON CG.[GroupId] = G.[Id]
INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CG.[CollectionId]
GO

CREATE OR ALTER VIEW [dbo].[CollectionUserPermissionsView]
AS
SELECT
    CU.[OrganizationUserId],
    CU.[CollectionId],
    C.[OrganizationId],
    C.[Name] AS [CollectionName],
    CU.[ReadOnly],
    CU.[HidePasswords],
    CU.[Manage]
FROM
    [dbo].[CollectionUser] CU
INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CU.[CollectionId]
GO

CREATE OR ALTER PROCEDURE [dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    -- Direct user-collection permissions
    SELECT
        OU.[Id] AS [UserGuid],
        OU.[UserId],
        OU.[Name] AS [UserName],
        OU.[Email],
        OU.[Status],
        OU.[AvatarColor],
        OU.[TwoFactorProviders],
        OU.[UsesKeyConnector],
        OU.[ResetPasswordKey],
        CUP.[CollectionId],
        CUP.[CollectionName],
        NULL AS [GroupId],
        NULL AS [GroupName],
        CUP.[ReadOnly],
        CUP.[HidePasswords],
        CUP.[Manage],
        CCD.[CipherId]
    FROM
        [dbo].[OrganizationUserUserDetailsView] OU
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[CollectionUserPermissionsView] CUP ON CUP.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        [dbo].[CollectionCipherDetailsView] CCD ON CCD.[CollectionId] = CUP.[CollectionId]
    WHERE
        O.[Id] = @OrganizationId
        AND O.[Enabled] = 1
        AND CUP.[OrganizationId] = @OrganizationId
        AND CCD.[CipherOrganizationId] = @OrganizationId
        AND OU.[Status] IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND CCD.[DeletedDate] IS NULL

    UNION ALL

    -- Group-based collection permissions
    SELECT
        OU.[Id] AS [UserGuid],
        OU.[UserId],
        OU.[Name] AS [UserName],
        OU.[Email],
        OU.[Status],
        OU.[AvatarColor],
        OU.[TwoFactorProviders],
        OU.[UsesKeyConnector],
        OU.[ResetPasswordKey],
        CGP.[CollectionId],
        CGP.[CollectionName],
        CGP.[GroupId],
        CGP.[GroupName],
        CGP.[ReadOnly],
        CGP.[HidePasswords],
        CGP.[Manage],
        CCD.[CipherId]
    FROM
        [dbo].[OrganizationUserUserDetailsView] OU
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[CollectionGroupPermissionsView] CGP ON CGP.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        [dbo].[CollectionCipherDetailsView] CCD ON CCD.[CollectionId] = CGP.[CollectionId]
    WHERE
        O.[Id] = @OrganizationId
        AND O.[Enabled] = 1
        AND CGP.[OrganizationId] = @OrganizationId
        AND CCD.[CipherOrganizationId] = @OrganizationId
        AND OU.[Status] IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND CCD.[DeletedDate] IS NULL

    UNION ALL

    -- Users without collection access
    SELECT
        OU.[Id] AS [UserGuid],
        OU.[UserId],
        OU.[Name] AS [UserName],
        OU.[Email],
        OU.[Status],
        OU.[AvatarColor],
        OU.[TwoFactorProviders],
        OU.[UsesKeyConnector],
        OU.[ResetPasswordKey],
        NULL AS [CollectionId],
        NULL AS [CollectionName],
        NULL AS [GroupId],
        NULL AS [GroupName],
        NULL AS [ReadOnly],
        NULL AS [HidePasswords],
        NULL AS [Manage],
        NULL AS [CipherId]
    FROM
        [dbo].[OrganizationUserUserDetailsView] OU
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    WHERE
        O.[Id] = @OrganizationId
        AND O.[Enabled] = 1
        AND OU.[Status] IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[CollectionUserPermissionsView] CUP
            WHERE CUP.[OrganizationUserId] = OU.[Id]
                AND CUP.[OrganizationId] = @OrganizationId
        )
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[CollectionGroupPermissionsView] CGP
            WHERE CGP.[OrganizationUserId] = OU.[Id]
                AND CGP.[OrganizationId] = @OrganizationId
        )
END
GO