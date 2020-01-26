CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_readcountbyorganizationid(par_organizationid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        COUNT(1)
        FROM vault_dbo.organizationuser
        WHERE organizationid = par_OrganizationId;
END;
$procedure$
;
