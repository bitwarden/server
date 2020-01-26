CREATE OR REPLACE FUNCTION vault_dbo.usercollectiondetails(par_userid uuid)
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
        FROM vault_dbo.collectionview AS c
        INNER JOIN vault_dbo.organizationuser AS ou
            ON c.organizationid = ou.organizationid
        INNER JOIN vault_dbo.organization AS o
            ON o.id = c.organizationid
        LEFT OUTER JOIN vault_dbo.collectionuser AS cu
            ON ou.accessall = 0 AND cu.collectionid = c.id AND cu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo.groupuser AS gu
            ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo."Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
            ON g.accessall = 0 AND cg.collectionid = c.id AND cg.groupid = gu.groupid
        WHERE ou.userid = par_UserId AND ou.status = 2 AND
        /* 2 = Confirmed */
        o.enabled = 1 AND (ou.accessall = 1 OR cu.collectionid IS NOT NULL OR g.accessall = 1 OR cg.collectionid IS NOT NULL);
END;
$function$
;
