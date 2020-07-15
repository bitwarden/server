CREATE OR REPLACE PROCEDURE "groupuser_readorganization_useridsbygroupid$tmp"(par_groupid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS GroupUser_ReadOrganizationUserIdsByGroupId$TMPTBL;
    CREATE TEMP TABLE GroupUser_ReadOrganizationUserIdsByGroupId$TMPTBL
    AS
    SELECT
        organization_userid
        FROM groupuser
        WHERE groupid = par_GroupId;
END;
$procedure$
;
