CREATE OR REPLACE PROCEDURE vault_dbo.user_readbypremium(par_premium numeric, INOUT p_refcur refcursor)
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
        FROM vault_dbo.userview
        WHERE premium = par_Premium;
END;
$procedure$
;
