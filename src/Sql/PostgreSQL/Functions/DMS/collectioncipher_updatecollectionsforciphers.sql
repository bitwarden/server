CREATE OR REPLACE PROCEDURE vault_dbo.collectioncipher_updatecollectionsforciphers(par_cipherids vault_dbo.guididarray, par_organizationid uuid, par_userid uuid, par_collectionids vault_dbo.guididarray)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CREATE TEMPORARY TABLE "#AvailableCollections"
    (id UUID);
    INSERT INTO "#AvailableCollections"
    SELECT
        c.id
        FROM vault_dbo.collection AS c
        INNER JOIN vault_dbo.organization AS o
            ON o.id = c.organizationid
        INNER JOIN vault_dbo.organizationuser AS ou
            ON ou.organizationid = o.id AND ou.userid = par_UserId
        LEFT OUTER JOIN vault_dbo.collectionuser AS cu
            ON ou.accessall = 0 AND cu.collectionid = c.id AND cu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo.groupuser AS gu
            ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
        LEFT OUTER JOIN vault_dbo."Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
            ON g.accessall = 0 AND cg.collectionid = c.id AND cg.groupid = gu.groupid
        WHERE o.id = par_OrganizationId AND o.enabled = 1 AND ou.status = 2 AND
        /* Confirmed */
        (ou.accessall = 1 OR cu.readonly = 0 OR g.accessall = 1 OR cg.readonly = 0);

    IF (SELECT
        COUNT(1)
        FROM "#AvailableCollections") < 1 THEN
        /* No writable collections available to share with in this organization. */
        RETURN;
    END IF;
    PERFORM vault_dbo.guididarray$aws$f('"par_CipherIds$aws$tmp"');
    INSERT INTO "par_CipherIds$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_CipherIds);
    INSERT INTO vault_dbo.collectioncipher (collectionid, cipherid)
    SELECT
        collection.id, cipher.id
        FROM "par_CollectionIds$aws$tmp" AS collection
        INNER JOIN "par_CipherIds$aws$tmp" AS cipher
            ON 1 = 1
        WHERE collection.id IN (SELECT
            id
            FROM "#AvailableCollections");
    CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationid(par_OrganizationId);
    /*

    DROP TABLE IF EXISTS "#AvailableCollections";
    */
    /*

    Temporary table must be removed before end of the function.
    */
END;
$procedure$
;
