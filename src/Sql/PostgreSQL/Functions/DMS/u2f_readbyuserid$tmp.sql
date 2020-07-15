CREATE OR REPLACE PROCEDURE "u2f_readbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS U2f_ReadByUserId$TMPTBL;
    CREATE TEMP TABLE U2f_ReadByUserId$TMPTBL
    AS
    SELECT
        *
        FROM u2fview
        WHERE userid = par_UserId;
END;
$procedure$
;
