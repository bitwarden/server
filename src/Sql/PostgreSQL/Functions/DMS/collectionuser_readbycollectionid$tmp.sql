CREATE OR REPLACE PROCEDURE "collectionuser_readbycollection_id$tmp"(par_collection_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS CollectionUser_ReadByCollectionId$TMPTBL;
    CREATE TEMP TABLE CollectionUser_ReadByCollectionId$TMPTBL
    AS
    SELECT
        organization_userid AS id, readonly
        FROM collectionuser
        WHERE collection_id = par_CollectionId;
END;
$procedure$
;
