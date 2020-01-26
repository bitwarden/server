CREATE OR REPLACE PROCEDURE vault_dbo."collection_readwithgroupsbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    collection_readbyid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.collection_readbyid(par_Id, p_refcur => collection_readbyid$refcur_1);
    DROP TABLE IF EXISTS Collection_ReadWithGroupsById$TMPTBL;
    CREATE TEMP TABLE Collection_ReadWithGroupsById$TMPTBL
    AS
    SELECT
        groupid AS id, readonly
        FROM vault_dbo.collectiongroup
        WHERE collectionid = par_Id;
END;
$procedure$
;
