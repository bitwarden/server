CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_readbyorganizationid(par_organizationid uuid, par_type numeric, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        *
        FROM vault_dbo.organizationuserview
        WHERE organizationid = par_OrganizationId AND (par_Type IS NULL OR type = par_Type);
END;
$procedure$
;
