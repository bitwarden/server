CREATE OR REPLACE PROCEDURE vault_dbo."user_readaccountrevisiondatebyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS User_ReadAccountRevisionDateById$TMPTBL;
    CREATE TEMP TABLE User_ReadAccountRevisionDateById$TMPTBL
    AS
    SELECT
        accountrevisiondate
        FROM vault_dbo."User"
        WHERE id = par_Id;
END;
$procedure$
;
