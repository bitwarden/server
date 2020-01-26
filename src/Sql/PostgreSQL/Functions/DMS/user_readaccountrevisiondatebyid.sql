CREATE OR REPLACE PROCEDURE vault_dbo.user_readaccountrevisiondatebyid(par_id uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        accountrevisiondate
        FROM vault_dbo."User"
        WHERE id = par_Id;
END;
$procedure$
;
