CREATE OR REPLACE PROCEDURE vault_dbo.groupuser_readgroupidsbyorganizationuserid(par_organizationuserid uuid, INOUT p_refcur refcursor)
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
        FROM vault_dbo.groupuser
        WHERE organizationuserid = par_OrganizationUserId;
END;
$procedure$
;
