CREATE OR ALTER PROC dbo.MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

SELECT
    U.Id AS UserGuid,
    U.Name AS UserName,
    U.Email,
    U.TwoFactorProviders,
    U.UsesKeyConnector,
    OU.ResetPasswordKey,
    CC.CollectionId,
    C.Name AS CollectionName,
    NULL AS GroupId,
    NULL AS GroupName,
    CU.ReadOnly,
    CU.HidePasswords,
    CU.Manage,
    Cipher.Id AS CipherId
FROM dbo.OrganizationUser OU
         INNER JOIN dbo.[User] U ON U.Id = OU.UserId
    INNER JOIN dbo.Organization O ON O.Id = OU.OrganizationId
    AND O.Id = @OrganizationId
    AND O.Enabled = 1
    INNER JOIN dbo.CollectionUser CU ON CU.OrganizationUserId = OU.Id
    INNER JOIN dbo.Collection C ON C.Id = CU.CollectionId
    INNER JOIN dbo.CollectionCipher CC ON CC.CollectionId = C.Id
    INNER JOIN dbo.Cipher Cipher ON Cipher.Id = CC.CipherId
    AND Cipher.OrganizationId = @OrganizationId
WHERE OU.Status in (0,1,2)
  AND Cipher.DeletedDate IS NULL

UNION ALL

-- Group-based collection permissions
SELECT
    U.Id AS UserGuid,
    U.Name AS UserName,
    U.Email,
    U.TwoFactorProviders,
    U.UsesKeyConnector,
    OU.ResetPasswordKey,
    CC.CollectionId,
    C.Name AS CollectionName,
    G.Id AS GroupId,
    G.Name AS GroupName,
    CG.ReadOnly,
    CG.HidePasswords,
    CG.Manage,
    Cipher.Id AS CipherId
FROM dbo.OrganizationUser OU
    INNER JOIN dbo.[User] U ON U.Id = OU.UserId
    INNER JOIN dbo.Organization O ON O.Id = OU.OrganizationId
    AND O.Id = @OrganizationId
    AND O.Enabled = 1
    INNER JOIN dbo.GroupUser GU ON GU.OrganizationUserId = OU.Id
    INNER JOIN dbo.[Group] G ON G.Id = GU.GroupId
    INNER JOIN dbo.CollectionGroup CG ON CG.GroupId = G.Id
    INNER JOIN dbo.Collection C ON C.Id = CG.CollectionId
    INNER JOIN dbo.CollectionCipher CC ON CC.CollectionId = C.Id
    INNER JOIN dbo.Cipher Cipher ON Cipher.Id = CC.CipherId
    AND Cipher.OrganizationId = @OrganizationId
WHERE OU.Status in (0,1,2)
  AND Cipher.DeletedDate IS NULL

GO
