CREATE OR REPLACE PROCEDURE vault_dbo."group_readwithcollectionsbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    group_readbyid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.group_readbyid(par_Id, p_refcur => group_readbyid$refcur_1);
    DROP TABLE IF EXISTS Group_ReadWithCollectionsById$TMPTBL;
    CREATE TEMP TABLE Group_ReadWithCollectionsById$TMPTBL
    AS
    SELECT
        collectionid AS id, readonly
        FROM vault_dbo.collectiongroup
        WHERE groupid = par_Id;
END;
$procedure$
;
