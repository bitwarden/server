CREATE OR REPLACE PROCEDURE groupuser_delete(par_groupid uuid, par_organization_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM groupuser
        WHERE groupid = par_GroupId AND organization_userid = par_OrganizationUserId;
    CALL user_bumpaccountrevisiondatebyorganization_userid(par_OrganizationUserId);
END;
$procedure$
;
