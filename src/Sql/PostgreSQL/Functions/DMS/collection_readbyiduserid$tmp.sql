CREATE OR REPLACE PROCEDURE vault_dbo."collection_readbyiduserid$tmp"(par_id uuid, par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM vault_dbo.usercollectiondetails(par_UserId);
    DROP TABLE IF EXISTS Collection_ReadByIdUserId$TMPTBL;
    CREATE TEMP TABLE Collection_ReadByIdUserId$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.usercollectiondetails$tmptbl
        WHERE Id = par_Id
        ORDER BY ReadOnly ASC NULLS FIRST
        LIMIT 1;
END;
$procedure$
;
