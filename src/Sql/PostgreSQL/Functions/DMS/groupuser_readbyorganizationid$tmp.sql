CREATE OR REPLACE PROCEDURE "groupuser_readbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS GroupUser_ReadByOrganizationId$TMPTBL;
    CREATE TEMP TABLE GroupUser_ReadByOrganizationId$TMPTBL
    AS
    SELECT
        gu.*
        FROM groupuser AS gu
        INNER JOIN "Group" AS g
            ON g.id = gu.groupid
        WHERE g.organizationid = par_OrganizationId;
END;
$procedure$
;
