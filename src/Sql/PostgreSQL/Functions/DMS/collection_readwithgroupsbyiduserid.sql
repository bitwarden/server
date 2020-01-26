CREATE OR REPLACE PROCEDURE vault_dbo.collection_readwithgroupsbyiduserid(par_id uuid, par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    collection_readbyiduserid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.collection_readbyiduserid(par_Id, par_UserId, p_refcur => collection_readbyiduserid$refcur_1);
    OPEN p_refcur FOR
    SELECT
        groupid AS id, readonly
        FROM vault_dbo.collectiongroup
        WHERE collectionid = par_Id;
END;
$procedure$
;
