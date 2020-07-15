CREATE OR REPLACE PROCEDURE folder_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_UserId UUID DEFAULT (SELECT
        userid
        FROM folder
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
        FROM organization_user
        WHERE userid = var_UserId AND status = 2
    /* Confirmed */
    )
    UPDATE cipher
    SET folders = JSON_MODIFY(c.folders, var_UserIdPath, NULL)
    FROM cipher AS c
    INNER JOIN cte AS ou
        ON c.userid IS NULL AND c.organizationid IN (SELECT
            organizationid
            FROM cte)
    INNER JOIN organization AS o
        ON o.id = ou.organizationid AND o.id = c.organizationid AND o.enabled = 1
    LEFT OUTER JOIN collectioncipher AS cc
        ON ou.accessall = 0 AND cc.cipherid = c.id
    LEFT OUTER JOIN collectionuser AS cu
        ON cu.collection_id = cc.collection_id AND cu.organization_userid = ou.id
    LEFT OUTER JOIN groupuser AS gu
        ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
    LEFT OUTER JOIN "Group" AS g
        ON g.id = gu.groupid
    LEFT OUTER JOIN collection_group AS cg
        ON g.accessall = 0 AND cg.collection_id = cc.collection_id AND cg.groupid = gu.groupid
        WHERE (ou.accessall = 1 OR cu.collection_id IS NOT NULL OR g.accessall = 1 OR cg.collection_id IS NOT NULL) AND JSON_VALUE(c.folders, var_UserIdPath) = par_Id;
    UPDATE cipher
    SET folders = JSON_MODIFY(c.folders, var_UserIdPath, NULL)
    FROM cipher AS c
        WHERE userid = var_UserId AND JSON_VALUE(folders, var_UserIdPath) = par_Id;
    DELETE FROM folder
        WHERE id = par_Id;
    CALL user_bumpaccountrevisiondate(var_UserId);
END;
$procedure$
;
