CREATE OR REPLACE PROCEDURE vault_dbo."groupuser_readgroupidsbyorganizationuserid$tmp"(par_organizationuserid uuid)
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
        FROM vault_dbo.groupuser
        WHERE organizationuserid = par_OrganizationUserId;
END;
$procedure$
;
