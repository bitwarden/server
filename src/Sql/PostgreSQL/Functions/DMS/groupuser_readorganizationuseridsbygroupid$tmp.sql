CREATE OR REPLACE PROCEDURE vault_dbo."groupuser_readorganizationuseridsbygroupid$tmp"(par_groupid uuid)
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
        organizationuserid
        FROM vault_dbo.groupuser
        WHERE groupid = par_GroupId;
END;
$procedure$
;
