CREATE OR REPLACE PROCEDURE vault_dbo.folder_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_UserId UUID DEFAULT (SELECT
        userid
        FROM vault_dbo.folder
        WHERE id = par_Id
        LIMIT 1);
    var_UserIdPath VARCHAR(50) DEFAULT CONCAT('$."', var_UserId, '"');
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    WITH cte
    AS (SELECT
        id, organizationid, accessall
        FROM vault_dbo.organizationuser
        WHERE userid = var_UserId AND status = 2
    /* Confirmed */
    )
    UPDATE vault_dbo.cipher
    SET folders = JSON_MODIFY(c.folders, var_UserIdPath, NULL)
    FROM vault_dbo.cipher AS c
    INNER JOIN cte AS ou
        ON c.userid IS NULL AND c.organizationid IN (SELECT
            organizationid
            FROM cte)
    INNER JOIN vault_dbo.organization AS o
        ON o.id = ou.organizationid AND o.id = c.organizationid AND o.enabled = 1
    LEFT OUTER JOIN vault_dbo.collectioncipher AS cc
        ON ou.accessall = 0 AND cc.cipherid = c.id
    LEFT OUTER JOIN vault_dbo.collectionuser AS cu
        ON cu.collectionid = cc.collectionid AND cu.organizationuserid = ou.id
    LEFT OUTER JOIN vault_dbo.groupuser AS gu
        ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
    LEFT OUTER JOIN vault_dbo."Group" AS g
        ON g.id = gu.groupid
    LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
        ON g.accessall = 0 AND cg.collectionid = cc.collectionid AND cg.groupid = gu.groupid
        WHERE (ou.accessall = 1 OR cu.collectionid IS NOT NULL OR g.accessall = 1 OR cg.collectionid IS NOT NULL) AND JSON_VALUE(c.folders, var_UserIdPath) = par_Id;
    UPDATE vault_dbo.cipher
    SET folders = JSON_MODIFY(c.folders, var_UserIdPath, NULL)
    FROM vault_dbo.cipher AS c
        WHERE userid = var_UserId AND JSON_VALUE(folders, var_UserIdPath) = par_Id;
    DELETE FROM vault_dbo.folder
        WHERE id = par_Id;
    CALL vault_dbo.user_bumpaccountrevisiondate(var_UserId);
END;
$procedure$
;
