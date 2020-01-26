CREATE OR REPLACE PROCEDURE vault_dbo."user_readkdfbyemail$tmp"(par_email character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS User_ReadKdfByEmail$TMPTBL;
    CREATE TEMP TABLE User_ReadKdfByEmail$TMPTBL
    AS
    SELECT
        kdf, kdfiterations
        FROM vault_dbo."User"
        WHERE LOWER(email) = LOWER(par_Email);
END;
$procedure$
;
