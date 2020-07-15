CREATE OR REPLACE PROCEDURE "folder_readbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Folder_ReadByUserId$TMPTBL;
    CREATE TEMP TABLE Folder_ReadByUserId$TMPTBL
    AS
    SELECT
        *
        FROM folderview
        WHERE userid = par_UserId;
END;
$procedure$
;
