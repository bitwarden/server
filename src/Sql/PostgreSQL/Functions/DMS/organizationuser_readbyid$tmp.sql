CREATE OR REPLACE PROCEDURE "organization_user_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUser_ReadById$TMPTBL;
    CREATE TEMP TABLE OrganizationUser_ReadById$TMPTBL
    AS
    SELECT
        *
        FROM organization_userview
        WHERE id = par_Id;
END;
$procedure$
;
