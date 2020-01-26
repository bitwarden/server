CREATE OR REPLACE PROCEDURE vault_dbo.groupuser_delete(par_groupid uuid, par_organizationuserid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo.groupuser
        WHERE groupid = par_GroupId AND organizationuserid = par_OrganizationUserId;
    CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationuserid(par_OrganizationUserId);
END;
$procedure$
;
