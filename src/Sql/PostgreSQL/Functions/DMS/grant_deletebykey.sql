CREATE OR REPLACE PROCEDURE vault_dbo.grant_deletebykey(par_key character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo."Grant"
        WHERE LOWER(key) = LOWER(par_Key);
END;
$procedure$
;
