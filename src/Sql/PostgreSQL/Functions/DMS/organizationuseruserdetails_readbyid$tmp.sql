CREATE OR REPLACE PROCEDURE vault_dbo."organizationuseruserdetails_readbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS OrganizationUserUserDetails_ReadById$TMPTBL;
    CREATE TEMP TABLE OrganizationUserUserDetails_ReadById$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.organizationuseruserdetailsview
        WHERE id = par_Id;
END;
$procedure$
;
