CREATE OR REPLACE PROCEDURE "organization_user_readcountbyorganizationidemail$tmp"(par_organizationid uuid, par_email character varying, par_onlyusers numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadCountByOrganizationIdEmail$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadCountByOrganizationIdEmail$TMPTBL
    AS
    SELECT
        COUNT(1) AS col1
        FROM organization_user AS ou
        LEFT OUTER JOIN "User" AS u
            ON ou.userid = u.id
        WHERE ou.organizationid = par_OrganizationId AND ((par_OnlyUsers = 0 AND (LOWER(ou.email) = LOWER(par_Email) OR LOWER(u.email) = LOWER(par_Email))) OR (par_OnlyUsers = 1 AND LOWER(u.email) = LOWER(par_Email)));
END;
$procedure$
;
