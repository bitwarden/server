CREATE OR REPLACE PROCEDURE vault_dbo."user_readbypremium$tmp"(par_premium numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS User_ReadByPremium$TMPTBL;
    CREATE TEMP TABLE User_ReadByPremium$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.userview
        WHERE premium = par_Premium;
END;
$procedure$
;
