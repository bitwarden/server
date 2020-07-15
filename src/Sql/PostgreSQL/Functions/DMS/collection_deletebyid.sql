CREATE OR REPLACE PROCEDURE collection_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrganizationId UUID DEFAULT (SELECT
        organizationid
        FROM collection
        WHERE id = par_Id
        LIMIT 1);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    IF var_OrganizationId IS NOT NULL THEN
        CALL user_bumpaccountrevisiondatebycollection_id(par_Id, var_OrganizationId);
    END IF;
    DELETE FROM collection_group
        WHERE collection_id = par_Id;
    DELETE FROM collection
        WHERE id = par_Id;
END;
$procedure$
;
