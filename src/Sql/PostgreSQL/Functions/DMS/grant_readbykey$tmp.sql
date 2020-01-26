CREATE OR REPLACE PROCEDURE vault_dbo."grant_readbykey$tmp"(par_key character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Grant_ReadByKey$TMPTBL;
    CREATE TEMP TABLE Grant_ReadByKey$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.grantview
        WHERE LOWER(key) = LOWER(par_Key);
END;
$procedure$
;
