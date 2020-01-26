CREATE OR REPLACE PROCEDURE vault_dbo.user_bumpaccountrevisiondate(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo."User"
    SET accountrevisiondate = timezone('UTC', CURRENT_TIMESTAMP(6))
        WHERE id = par_Id;
END;
$procedure$
;
