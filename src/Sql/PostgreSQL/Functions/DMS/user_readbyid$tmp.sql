CREATE OR REPLACE PROCEDURE vault_dbo."user_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS User_ReadById$TMPTBL;
    CREATE TEMP TABLE User_ReadById$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.userview
        WHERE id = par_Id;
END;
$procedure$
;
