CREATE OR REPLACE PROCEDURE "organization_user_readbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadByUserId$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadByUserId$TMPTBL
    AS
    SELECT
        *
        FROM organization_userview
        WHERE userid = par_UserId;
END;
$procedure$
;
