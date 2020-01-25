CREATE OR REPLACE PROCEDURE vault_dbo."cipherorganizationdetails_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS CipherOrganizationDetails_ReadById$TMPTBL;
    CREATE TEMP TABLE CipherOrganizationDetails_ReadById$TMPTBL
    AS
    SELECT
        c.*,
        CASE
            WHEN o.usetotp = 1 THEN 1
            ELSE 0
        END AS organizationusetotp
        FROM vault_dbo.cipherview AS c
        LEFT OUTER JOIN vault_dbo.organization AS o
            ON o.id = c.organizationid
        WHERE c.id = par_Id;
END;
$procedure$
;
