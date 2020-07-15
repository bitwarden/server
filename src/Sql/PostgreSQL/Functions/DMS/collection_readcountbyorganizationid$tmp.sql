CREATE OR REPLACE PROCEDURE "collection_readcountbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Collection_ReadCountByOrganizationId$TMPTBL;
    CREATE TEMP TABLE Collection_ReadCountByOrganizationId$TMPTBL
    AS
    SELECT
        COUNT(1) AS col1
        FROM collection
        WHERE organizationid = par_OrganizationId;
END;
$procedure$
;
