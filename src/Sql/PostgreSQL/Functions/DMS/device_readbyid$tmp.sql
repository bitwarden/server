CREATE OR REPLACE PROCEDURE vault_dbo."device_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Device_ReadById$TMPTBL;
    CREATE TEMP TABLE Device_ReadById$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.deviceview
        WHERE id = par_Id;
END;
$procedure$
;
