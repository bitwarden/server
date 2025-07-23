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
        WHERE
            OU.UserId = @UserId
            AND OU.Status = 2
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
            AND CU.ReadOnly = 0
    ),
    GroupCollectionAccess AS (
        SELECT
            CC.CipherId
        FROM
            dbo.OrganizationUser OU
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
        SELECT CipherId
        FROM UserCollectionAccess

        UNION ALL

        SELECT GC.CipherId
        FROM GroupCollectionAccess AS GC
        WHERE NOT EXISTS (
            SELECT 1
            FROM UserCollectionAccess AS UA
            WHERE UA.CipherId = GC.CipherId
        )
    ),
    SecurityTasks AS (
        SELECT
            ST.*
        FROM
            dbo.[SecurityTaskView] ST
        WHERE
            @Status IS NULL
            OR ST.Status = @Status
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
        SecurityTasks ST
        JOIN OrganizationAccess OA
            ON ST.OrganizationId = OA.OrganizationId
        LEFT JOIN AccessibleCiphers AC
            ON ST.CipherId = AC.CipherId
    WHERE
        ST.CipherId IS NULL
        OR AC.CipherId IS NOT NULL
    ORDER BY
        ST.CreationDate DESC;
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
    WHERE Status = 2;
END