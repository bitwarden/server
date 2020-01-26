CREATE OR REPLACE PROCEDURE vault_dbo.collection_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrganizationId UUID DEFAULT (SELECT
        organizationid
        FROM vault_dbo.collection
        WHERE id = par_Id
        LIMIT 1);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    IF var_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.user_bumpaccountrevisiondatebycollectionid(par_Id, var_OrganizationId);
    END IF;
    DELETE FROM vault_dbo.collectiongroup
        WHERE collectionid = par_Id;
    DELETE FROM vault_dbo.collection
        WHERE id = par_Id;
END;
$procedure$
;
