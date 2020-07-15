CREATE OR REPLACE PROCEDURE "organization_user_readbyorganizationiduserid$tmp"(par_organizationid uuid, par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadByOrganizationIdUserId$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadByOrganizationIdUserId$TMPTBL
    AS
    SELECT
        *
        FROM organization_userview
        WHERE organizationid = par_OrganizationId AND userid = par_UserId;
END;
$procedure$
;
