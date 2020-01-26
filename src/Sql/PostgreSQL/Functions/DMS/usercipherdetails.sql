CREATE OR REPLACE FUNCTION vault_dbo.usercipherdetails(par_userid uuid)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
-- Converted with error!
    -- BEGIN
    --    DROP TABLE IF EXISTS UserCipherDetails$TMPTBL;
    --    PERFORM vault_dbo.cipherdetails(par_UserId);
    --    PERFORM vault_dbo.cipherdetails(par_UserId);
    --    CREATE TEMPORARY TABLE UserCipherDetails$TMPTBL
    --    AS
    --    WITH cte
    --    AS (SELECT
    --        id, organizationid, accessall
    --        FROM vault_dbo.organizationuser
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
    --        FROM vault_dbo.cipherdetails$tmptbl
    --        INNER JOIN
    --        /* Transformer error occurred */
    --        INNER JOIN vault_dbo.organization AS o
    --            ON o.id = ou.organizationid AND o.id = c.OrganizationId AND o.enabled = 1
    --        LEFT OUTER JOIN vault_dbo.collectioncipher AS cc
    --            ON ou.accessall = 0 AND cc.cipherid = c.Id
    --        LEFT OUTER JOIN vault_dbo.collectionuser AS cu
    --            ON cu.collectionid = cc.collectionid AND cu.organizationuserid = ou.id
    --        LEFT OUTER JOIN vault_dbo.groupuser AS gu
    --            ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
    --        LEFT OUTER JOIN vault_dbo."Group" AS g
    --            ON g.id = gu.groupid
    --        LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
    --            ON g.accessall = 0 AND cg.collectionid = cc.collectionid AND cg.groupid = gu.groupid
    --        WHERE ou.accessall = 1 OR cu.collectionid IS NOT NULL OR g.accessall = 1 OR cg.collectionid IS NOT NULL
    --    UNION ALL
    --    SELECT
    --        *, 1 AS edit, 0 AS organizationusetotp
    --        FROM vault_dbo.cipherdetails$tmptbl
    --        WHERE userid = par_UserId;
    -- END;
END;
$function$
;
