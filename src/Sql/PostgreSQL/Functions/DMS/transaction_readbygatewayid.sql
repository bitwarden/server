CREATE OR REPLACE PROCEDURE transaction_readbygatewayid(par_gateway numeric, par_gatewayid character varying, INOUT p_refcur refcursor)
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
        FROM transactionview
        WHERE gateway = par_Gateway AND LOWER(gatewayid) = LOWER(par_GatewayId);
END;
$procedure$
;
