CREATE OR REPLACE PROCEDURE "transaction_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Transaction_ReadById$TMPTBL;
    CREATE TEMP TABLE Transaction_ReadById$TMPTBL
    AS
    SELECT
        *
        FROM transactionview
        WHERE id = par_Id;
END;
$procedure$
;
