CREATE OR REPLACE PROCEDURE collectioncipher_readbyuseridcipherid(par_userid uuid, par_cipherid uuid, INOUT p_refcur refcursor)
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
        FROM collectioncipher AS cc
        INNER JOIN collection AS s
            ON s.id = cc.collection_id
        INNER JOIN organization_user AS ou
            ON ou.organizationid = s.organizationid AND ou.userid = par_UserId
        LEFT OUTER JOIN collectionuser AS cu
            ON ou.accessall = 0 AND cu.collection_id = s.id AND cu.organization_userid = ou.id
        LEFT OUTER JOIN groupuser AS gu
            ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
        LEFT OUTER JOIN "Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN collection_group AS cg
            ON g.accessall = 0 AND cg.collection_id = cc.collection_id AND cg.groupid = gu.groupid
        WHERE cc.cipherid = par_CipherId AND ou.status = 2 AND
        /* Confirmed */
        (ou.accessall = 1 OR cu.collection_id IS NOT NULL OR g.accessall = 1 OR cg.collection_id IS NOT NULL);
END;
$procedure$
;
