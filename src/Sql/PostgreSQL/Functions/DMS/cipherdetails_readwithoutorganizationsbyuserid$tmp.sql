CREATE OR REPLACE PROCEDURE "cipherdetails_readwithoutorganizationsbyuserid$tmp"(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM cipherdetails(par_UserId);
    DROP TABLE IF EXISTS CipherDetails_ReadWithoutOrganizationsByUserId$TMPTBL;
    CREATE TEMP TABLE CipherDetails_ReadWithoutOrganizationsByUserId$TMPTBL
    AS
    SELECT
        *, 1 AS edit, 0 AS organizationusetotp
        FROM cipherdetails$tmptbl
        WHERE UserId = par_UserId;
END;
$procedure$
;
