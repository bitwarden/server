CREATE OR REPLACE PROCEDURE "group_readwithcollectionsbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    group_readbyid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL group_readbyid(par_Id, p_refcur => group_readbyid$refcur_1);
    DROP TABLE IF EXISTS Group_ReadWithCollectionsById$TMPTBL;
    CREATE TEMP TABLE Group_ReadWithCollectionsById$TMPTBL
    AS
    SELECT
        collection_id AS id, readonly
        FROM collection_group
        WHERE groupid = par_Id;
END;
$procedure$
;
