CREATE OR REPLACE PROCEDURE collectioncipher_delete(par_collection_id uuid, par_cipherid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrganizationId UUID DEFAULT (SELECT
        organizationid
        FROM cipher
        WHERE id = par_CipherId
        LIMIT 1);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM collectioncipher
        WHERE collection_id = par_CollectionId AND cipherid = par_CipherId;

    IF var_OrganizationId IS NOT NULL THEN
        CALL user_bumpaccountrevisiondatebycollection_id(par_CollectionId, var_OrganizationId);
    END IF;
END;
$procedure$
;
