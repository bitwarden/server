CREATE OR REPLACE PROCEDURE groupuser_readgroupidsbyorganization_userid(par_organization_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        groupid
        FROM groupuser
        WHERE organization_userid = par_OrganizationUserId;
END;
$procedure$
;
