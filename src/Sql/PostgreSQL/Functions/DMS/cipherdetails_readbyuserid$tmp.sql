CREATE OR REPLACE PROCEDURE "cipherdetails_readbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM usercipherdetails(par_UserId);
    DROP TABLE IF EXISTS CipherDetails_ReadByUserId$TMPTBL;
    CREATE TEMP TABLE CipherDetails_ReadByUserId$TMPTBL
    AS
    SELECT
        *
        FROM usercipherdetails$tmptbl;
END;
$procedure$
;
