CREATE OR REPLACE PROCEDURE vault_dbo.collectioncipher_delete(par_collectionid uuid, par_cipherid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrganizationId UUID DEFAULT (SELECT
        organizationid
        FROM vault_dbo.cipher
        WHERE id = par_CipherId
        LIMIT 1);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo.collectioncipher
        WHERE collectionid = par_CollectionId AND cipherid = par_CipherId;

    IF var_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.user_bumpaccountrevisiondatebycollectionid(par_CollectionId, var_OrganizationId);
    END IF;
END;
$procedure$
;
