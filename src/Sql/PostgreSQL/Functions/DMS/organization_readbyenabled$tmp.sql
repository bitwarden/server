CREATE OR REPLACE PROCEDURE vault_dbo."organization_readbyenabled$tmp"()
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Organization_ReadByEnabled$TMPTBL;
    CREATE TEMP TABLE Organization_ReadByEnabled$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.organizationview
        WHERE enabled = 1;
END;
$procedure$
;
