CREATE OR REPLACE PROCEDURE "organization_user_readbyorganizationid$tmp"(par_organizationid uuid, par_type numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadByOrganizationId$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadByOrganizationId$TMPTBL
    AS
    SELECT
        *
        FROM organization_userview
        WHERE organizationid = par_OrganizationId AND (par_Type IS NULL OR type = par_Type);
END;
$procedure$
;
