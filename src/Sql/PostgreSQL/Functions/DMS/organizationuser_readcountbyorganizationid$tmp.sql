CREATE OR REPLACE PROCEDURE "organization_user_readcountbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadCountByOrganizationId$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadCountByOrganizationId$TMPTBL
    AS
    SELECT
        COUNT(1) AS col1
        FROM organization_user
        WHERE organizationid = par_OrganizationId;
END;
$procedure$
;
