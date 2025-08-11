CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH OrganizationAccess AS (
        SELECT
            OU.OrganizationId
        FROM
            dbo.OrganizationUser OU
        INNER JOIN dbo.OrganizationView O
            ON O.Id = OU.OrganizationId
        WHERE
            OU.UserId = @UserId
            AND OU.Status = 2
            AND O.Enabled = 1
    ),
    UserCollectionAccess AS (
        SELECT
            CC.CipherId
        FROM
            dbo.OrganizationUser OU
            JOIN dbo.CollectionUser CU
                ON CU.OrganizationUserId = OU.Id
            JOIN dbo.CollectionCipher CC
                ON CC.CollectionId = CU.CollectionId
        WHERE
            OU.UserId = @UserId
            AND OU.Status = 2
            AND O.Enabled = 1
            AND CU.ReadOnly = 0
    ),
    GroupCollectionAccess AS (
        SELECT
            CC.CipherId
        FROM
            dbo.OrganizationUser OU
            INNER JOIN dbo.OrganizationView O
                ON O.Id = OU.OrganizationId
            JOIN dbo.GroupUser GU
                ON GU.OrganizationUserId = OU.Id
            JOIN dbo.CollectionGroup CG
                ON CG.GroupId = GU.GroupId
            JOIN dbo.CollectionCipher CC
                ON CC.CollectionId = CG.CollectionId
        WHERE
            OU.UserId = @UserId
            AND OU.Status = 2
            AND CG.ReadOnly = 0
    ),
    AccessibleCiphers AS (
        SELECT
            CipherId FROM UserCollectionAccess
        UNION
        SELECT
            CipherId FROM GroupCollectionAccess
    )
    SELECT
        ST.Id,
        ST.OrganizationId,
        ST.CipherId,
        ST.Type,
        ST.Status,
        ST.CreationDate,
        ST.RevisionDate
    FROM
      dbo.[SecurityTaskView] ST
      INNER JOIN OrganizationAccess OA
          ON ST.OrganizationId = OA.OrganizationId
    WHERE
        (@Status IS NULL OR ST.Status = @Status)
        AND (
          ST.CipherId IS NULL
          OR EXISTS (
              SELECT 1
              FROM AccessibleCiphers AC
              WHERE AC.CipherId = ST.CipherId
          )
        )
    ORDER BY
        ST.CreationDate DESC
    OPTION (RECOMPILE);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CollectionGroup')
        AND name = 'IX_CollectionGroup_GroupId_ReadOnly'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CollectionGroup_GroupId_ReadOnly
      ON dbo.CollectionGroup (GroupId, ReadOnly)
      INCLUDE (CollectionId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CollectionUser')
        AND name = 'IX_CollectionUser_OrganizationUserId_ReadOnly'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CollectionUser_OrganizationUserId_ReadOnly
      ON dbo.CollectionUser (OrganizationUserId, ReadOnly)
      INCLUDE (CollectionId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.SecurityTask')
        AND name = 'IX_SecurityTask_Status_OrgId_CreationDateDesc'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SecurityTask_Status_OrgId_CreationDateDesc
      ON dbo.SecurityTask (Status, OrganizationId, CreationDate DESC)
      INCLUDE (CipherId, [Type], RevisionDate);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CollectionCipher')
        AND name = 'IX_CollectionCipher_CollectionId_CipherId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CollectionCipher_CollectionId_CipherId
        ON dbo.CollectionCipher (CollectionId, CipherId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.OrganizationUser')
        AND name = 'IX_OrganizationUser_UserId_Status_Filtered'
)
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationUser_UserId_Status_Filtered]
    ON [dbo].[OrganizationUser] (UserId)
    INCLUDE (Id, OrganizationId)
    WHERE Status = 2;
END
