CREATE OR REPLACE PROCEDURE collectioncipher_updatecollectionsforciphers(par_cipherids guididarray, par_organizationid uuid, par_userid uuid, par_collection_ids guididarray)
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
        FROM collection AS c
        INNER JOIN organization AS o
            ON o.id = c.organizationid
        INNER JOIN organization_user AS ou
            ON ou.organizationid = o.id AND ou.userid = par_UserId
        LEFT OUTER JOIN collectionuser AS cu
            ON ou.accessall = 0 AND cu.collection_id = c.id AND cu.organization_userid = ou.id
        LEFT OUTER JOIN groupuser AS gu
            ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
        LEFT OUTER JOIN "Group" AS g
            ON g.id = gu.groupid
        LEFT OUTER JOIN collection_group AS cg
            ON g.accessall = 0 AND cg.collection_id = c.id AND cg.groupid = gu.groupid
        WHERE o.id = par_OrganizationId AND o.enabled = 1 AND ou.status = 2 AND
        /* Confirmed */
        (ou.accessall = 1 OR cu.readonly = 0 OR g.accessall = 1 OR cg.readonly = 0);

    IF (SELECT
        COUNT(1)
        FROM "#AvailableCollections") < 1 THEN
        /* No writable collections available to share with in this organization. */
        RETURN;
    END IF;
    PERFORM guididarray$aws$f('"par_CipherIds$aws$tmp"');
    INSERT INTO "par_CipherIds$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_CipherIds);
    INSERT INTO collectioncipher (collection_id, cipherid)
    SELECT
        collection.id, cipher.id
        FROM "par_CollectionIds$aws$tmp" AS collection
        INNER JOIN "par_CipherIds$aws$tmp" AS cipher
            ON 1 = 1
        WHERE collection.id IN (SELECT
            id
            FROM "#AvailableCollections");
    CALL user_bumpaccountrevisiondatebyorganizationid(par_OrganizationId);
    /*

    DROP TABLE IF EXISTS "#AvailableCollections";
    */
    /*

    Temporary table must be removed before end of the function.
    */
END;
$procedure$
;
