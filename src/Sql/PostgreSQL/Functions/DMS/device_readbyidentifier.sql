CREATE OR REPLACE PROCEDURE vault_dbo.device_readbyidentifier(par_identifier character varying, INOUT p_refcur refcursor)
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
        FROM vault_dbo.deviceview
        WHERE LOWER(identifier) = LOWER(par_Identifier);
END;
$procedure$
;
