CREATE OR REPLACE PROCEDURE vault_dbo.cipher_updatecollections(par_id uuid, par_userid uuid, par_organizationid uuid, par_collectionids vault_dbo.guididarray, INOUT return_code integer)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    IF par_OrganizationId IS NULL OR (SELECT
        COUNT(1)
        FROM "par_CollectionIds$aws$tmp") < 1 THEN
        return_code := (- 1);
        RETURN;
    END IF;
    CREATE TEMPORARY TABLE "#AvailableCollections"
    (id UUID);

    IF par_UserId IS NULL THEN
        INSERT INTO "#AvailableCollections"
        SELECT
            id
            FROM vault_dbo.collection
            WHERE organizationid = par_OrganizationId;
    ELSE
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
    END IF;

    IF (SELECT
        COUNT(1)
        FROM "#AvailableCollections") < 1 THEN
        /* No writable collections available to share with in this organization. */
        return_code := (- 1);
        RETURN;
    END IF;
    INSERT INTO vault_dbo.collectioncipher (collectionid, cipherid)
    SELECT
        id, par_Id
        FROM "par_CollectionIds$aws$tmp"
        WHERE id IN (SELECT
            id
            FROM "#AvailableCollections");
    return_code := (0);
    RETURN;
    /*

    DROP TABLE IF EXISTS "#AvailableCollections";
    */
    /*

    Temporary table must be removed before end of the function.
    */
END;
$procedure$
;
