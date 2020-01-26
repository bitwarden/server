CREATE OR REPLACE PROCEDURE vault_dbo."collectionuser_readbycollectionid$tmp"(par_collectionid uuid)
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
        organizationuserid AS id, readonly
        FROM vault_dbo.collectionuser
        WHERE collectionid = par_CollectionId;
END;
$procedure$
;
