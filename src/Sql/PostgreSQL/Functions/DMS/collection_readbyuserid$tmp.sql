CREATE OR REPLACE PROCEDURE vault_dbo."collection_readbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM vault_dbo.usercollectiondetails(par_UserId);
    DROP TABLE IF EXISTS Collection_ReadByUserId$TMPTBL;
    CREATE TEMP TABLE Collection_ReadByUserId$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.usercollectiondetails$tmptbl;
END;
$procedure$
;
