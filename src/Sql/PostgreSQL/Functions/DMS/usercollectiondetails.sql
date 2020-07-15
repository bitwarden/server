CREATE OR REPLACE FUNCTION user_collection_details(par_user_id uuid)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
    DROP TABLE IF EXISTS UserCollectionDetails$TMPTBL;
    CREATE TEMPORARY TABLE UserCollectionDetails$TMPTBL
    AS
    SELECT
        c.*,
        CASE
            WHEN ou.accessall = 1 OR g.accessall = 1 OR cu.readonly = 0 OR cg.readonly = 0 THEN 0
            ELSE 1
        END AS readonly
        FROM collectionview AS c
        INNER JOIN organization_user AS ou
            ON c.organizationid = ou.organizationid
        INNER JOIN organization AS o
            ON o.id = c.organizationid
        LEFT OUTER JOIN collectionuser AS cu
            ON ou.accessall = 0 AND cu.collection_id = c.id AND cu.organization_userid = ou.id
        LEFT OUTER JOIN groupuser AS gu
            ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
        LEFT OUTER JOIN "Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN collection_group AS cg
            ON g.accessall = 0 AND cg.collection_id = c.id AND cg.groupid = gu.groupid
        WHERE ou.userid = par_UserId AND ou.status = 2 AND
        /* 2 = Confirmed */
        o.enabled = 1 AND (ou.accessall = 1 OR cu.collection_id IS NOT NULL OR g.accessall = 1 OR cg.collection_id IS NOT NULL);
END;
$function$
;
