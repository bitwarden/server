CREATE PROCEDURE [dbo].[SecurityTask_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT = NULL
WITH RECOMPILE
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
