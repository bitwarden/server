CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationuserid(par_Id);
    DELETE FROM vault_dbo.collectionuser
        WHERE organizationuserid = par_Id;
    DELETE FROM vault_dbo.groupuser
        WHERE organizationuserid = par_Id;
    DELETE FROM vault_dbo.organizationuser
        WHERE id = par_Id;
END;
$procedure$
;
