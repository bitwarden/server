CREATE OR REPLACE PROCEDURE "group_readbyorganizationid$tmp"(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Group_ReadByOrganizationId$TMPTBL;
    CREATE TEMP TABLE Group_ReadByOrganizationId$TMPTBL
    AS
    SELECT
        *
        FROM groupview
        WHERE organizationid = par_OrganizationId;
END;
$procedure$
;
