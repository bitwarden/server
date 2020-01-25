CREATE OR REPLACE PROCEDURE vault_dbo."cipher_readbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Cipher_ReadByOrganizationId$TMPTBL;
    CREATE TEMP TABLE Cipher_ReadByOrganizationId$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.cipherview
        WHERE userid IS NULL AND organizationid = par_OrganizationId;
END;
$procedure$
;
