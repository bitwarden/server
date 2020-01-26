CREATE OR REPLACE PROCEDURE vault_dbo.groupuser_readorganizationuseridsbygroupid(par_groupid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        organizationuserid
        FROM vault_dbo.groupuser
        WHERE groupid = par_GroupId;
END;
$procedure$
;
