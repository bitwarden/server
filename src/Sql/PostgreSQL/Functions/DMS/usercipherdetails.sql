CREATE OR REPLACE FUNCTION usercipherdetails(par_userid uuid)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
-- Converted with error!
    -- BEGIN
    --    DROP TABLE IF EXISTS UserCipherDetails$TMPTBL;
    --    PERFORM cipherdetails(par_UserId);
    --    PERFORM cipherdetails(par_UserId);
    --    CREATE TEMPORARY TABLE UserCipherDetails$TMPTBL
    --    AS
    --    WITH cte
    --    AS (SELECT
    --        id, organizationid, accessall
    --        FROM organization_user
    --        WHERE userid = par_UserId AND status = 2
    --    /* Confirmed */
    --    )
    --    SELECT
    --        c.*,
    --        CASE
    --            WHEN ou.accessall = 1 OR cu.readonly = 0 OR g.accessall = 1 OR cg.readonly = 0 THEN 1
    --            ELSE 0
    --        END AS edit,
    --        CASE
    --            WHEN o.usetotp = 1 THEN 1
    --            ELSE 0
    --        END AS organizationusetotp
    --        FROM cipherdetails$tmptbl
    --        INNER JOIN
    --        /* Transformer error occurred */
    --        INNER JOIN organization AS o
    --            ON o.id = ou.organizationid AND o.id = c.OrganizationId AND o.enabled = 1
    --        LEFT OUTER JOIN collectioncipher AS cc
    --            ON ou.accessall = 0 AND cc.cipherid = c.Id
    --        LEFT OUTER JOIN collectionuser AS cu
    --            ON cu.collection_id = cc.collection_id AND cu.organization_userid = ou.id
    --        LEFT OUTER JOIN groupuser AS gu
    --            ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
    --        LEFT OUTER JOIN "Group" AS g
    --            ON g.id = gu.groupid
    --        LEFT OUTER JOIN collection_group AS cg
    --            ON g.accessall = 0 AND cg.collection_id = cc.collection_id AND cg.groupid = gu.groupid
    --        WHERE ou.accessall = 1 OR cu.collection_id IS NOT NULL OR g.accessall = 1 OR cg.collection_id IS NOT NULL
    --    UNION ALL
    --    SELECT
    --        *, 1 AS edit, 0 AS organizationusetotp
    --        FROM cipherdetails$tmptbl
    --        WHERE userid = par_UserId;
    -- END;
END;
$function$
;
