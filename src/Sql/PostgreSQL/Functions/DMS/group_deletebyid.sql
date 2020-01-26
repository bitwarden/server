CREATE OR REPLACE PROCEDURE vault_dbo.group_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrganizationId UUID DEFAULT (SELECT
        organizationid
        FROM vault_dbo."Group"
        WHERE id = par_Id
        LIMIT 1);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    IF var_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationid(var_OrganizationId);
    END IF;
    DELETE FROM vault_dbo."Group"
        WHERE id = par_Id;
END;
$procedure$
;
