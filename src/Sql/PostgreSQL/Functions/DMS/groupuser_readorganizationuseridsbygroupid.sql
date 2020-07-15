CREATE OR REPLACE PROCEDURE groupuser_readorganization_useridsbygroupid(par_groupid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        organization_userid
        FROM groupuser
        WHERE groupid = par_GroupId;
END;
$procedure$
;
