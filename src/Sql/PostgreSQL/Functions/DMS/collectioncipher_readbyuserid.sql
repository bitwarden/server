CREATE OR REPLACE PROCEDURE vault_dbo.collectioncipher_readbyuserid(par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        cc.*
        FROM vault_dbo.collectioncipher AS cc
        INNER JOIN vault_dbo.collection AS s
            ON s.id = cc.collectionid
        INNER JOIN vault_dbo.organizationuser AS ou
            ON ou.organizationid = s.organizationid AND ou.userid = par_UserId
        LEFT OUTER JOIN vault_dbo.collectionuser AS cu
            ON ou.accessall = 0 AND cu.collectionid = s.id AND cu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo.groupuser AS gu
            ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo."Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
            ON g.accessall = 0 AND cg.collectionid = cc.collectionid AND cg.groupid = gu.groupid
        WHERE ou.status = 2 AND
        /* Confirmed */
        (ou.accessall = 1 OR cu.collectionid IS NOT NULL OR g.accessall = 1 OR cg.collectionid IS NOT NULL);
END;
$procedure$
;
