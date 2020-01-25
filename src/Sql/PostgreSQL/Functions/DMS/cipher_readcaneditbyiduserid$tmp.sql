CREATE OR REPLACE PROCEDURE vault_dbo."cipher_readcaneditbyiduserid$tmp"(par_id uuid, par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_CanEdit NUMERIC(1, 0);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    WITH cte
    AS (SELECT
        CASE
            WHEN c.userid IS NOT NULL OR ou.accessall = 1 OR cu.readonly = 0 OR g.accessall = 1 OR cg.readonly = 0 THEN 1
            ELSE 0
        END AS edit
        FROM vault_dbo.cipher AS c
        LEFT OUTER JOIN vault_dbo.organization AS o
            ON c.userid IS NULL AND o.id = c.organizationid
        LEFT OUTER JOIN vault_dbo.organizationuser AS ou
            ON ou.organizationid = o.id AND ou.userid = par_UserId
        LEFT OUTER JOIN vault_dbo.collectioncipher AS cc
            ON c.userid IS NULL AND ou.accessall = 0 AND cc.cipherid = c.id
        LEFT OUTER JOIN vault_dbo.collectionuser AS cu
            ON cu.collectionid = cc.collectionid AND cu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo.groupuser AS gu
            ON c.userid IS NULL AND cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo."Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
            ON g.accessall = 0 AND cg.collectionid = cc.collectionid AND cg.groupid = gu.groupid
        WHERE c.id = par_Id AND (c.userid = par_UserId OR (c.userid IS NULL AND ou.status = 2 AND
        /* 2 = Confirmed */
        o.enabled = 1 AND (ou.accessall = 1 OR cu.collectionid IS NOT NULL OR g.accessall = 1 OR cg.collectionid IS NOT NULL))))
    SELECT
        CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
        INTO var_CanEdit
        FROM cte
        WHERE edit = 1;
    DROP TABLE IF EXISTS Cipher_ReadCanEditByIdUserId$TMPTBL;
    CREATE TEMP TABLE Cipher_ReadCanEditByIdUserId$TMPTBL
    AS
    SELECT
        var_CanEdit;
END;
$procedure$
;
