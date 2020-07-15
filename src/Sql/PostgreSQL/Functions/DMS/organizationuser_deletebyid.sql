CREATE OR REPLACE PROCEDURE organization_user_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL user_bumpaccountrevisiondatebyorganization_userid(par_Id);
    DELETE FROM collectionuser
        WHERE organization_userid = par_Id;
    DELETE FROM groupuser
        WHERE organization_userid = par_Id;
    DELETE FROM organization_user
        WHERE id = par_Id;
END;
$procedure$
;
