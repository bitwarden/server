CREATE OR REPLACE PROCEDURE "groupuser_readgroupidsbyorganization_userid$tmp"(par_organization_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS GroupUser_ReadGroupIdsByOrganizationUserId$TMPTBL;
    CREATE TEMP TABLE GroupUser_ReadGroupIdsByOrganizationUserId$TMPTBL
    AS
    SELECT
        groupid
        FROM groupuser
        WHERE organization_userid = par_OrganizationUserId;
END;
$procedure$
;
