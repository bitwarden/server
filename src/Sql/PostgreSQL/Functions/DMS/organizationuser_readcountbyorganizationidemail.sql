CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_readcountbyorganizationidemail(par_organizationid uuid, par_email character varying, par_onlyusers numeric, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        COUNT(1)
        FROM vault_dbo.organizationuser AS ou
        LEFT OUTER JOIN vault_dbo."User" AS u
            ON ou.userid = u.id
        WHERE ou.organizationid = par_OrganizationId AND ((par_OnlyUsers = 0 AND (LOWER(ou.email) = LOWER(par_Email) OR LOWER(u.email) = LOWER(par_Email))) OR (par_OnlyUsers = 1 AND LOWER(u.email) = LOWER(par_Email)));
END;
$procedure$
;
