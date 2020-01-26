CREATE OR REPLACE PROCEDURE vault_dbo."organizationuser_readcountbyfreeorganizationadminuser$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadCountByFreeOrganizationAdminUser$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadCountByFreeOrganizationAdminUser$TMPTBL
    AS
    SELECT
        COUNT(1) AS col1
        FROM vault_dbo.organizationuser AS ou
        INNER JOIN vault_dbo.organization AS o
            ON o.id = ou.organizationid
        WHERE ou.userid = par_UserId AND ou.type < 2 AND
        /* Owner or Admin */
        o.plantype = 0 AND
        /* Free */
        ou.status = 2;
END;
$procedure$
;
