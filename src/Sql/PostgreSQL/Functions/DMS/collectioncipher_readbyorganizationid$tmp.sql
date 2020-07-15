CREATE OR REPLACE PROCEDURE "collectioncipher_readbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS CollectionCipher_ReadByOrganizationId$TMPTBL;
    CREATE TEMP TABLE CollectionCipher_ReadByOrganizationId$TMPTBL
    AS
    SELECT
        sc.*
        FROM collectioncipher AS sc
        INNER JOIN collection AS s
            ON s.id = sc.collection_id
        WHERE s.organizationid = par_OrganizationId;
END;
$procedure$
;
