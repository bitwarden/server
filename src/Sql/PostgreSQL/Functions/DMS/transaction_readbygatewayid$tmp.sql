CREATE OR REPLACE PROCEDURE "transaction_readbygatewayid$tmp"(par_gateway numeric, par_gatewayid character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Transaction_ReadByGatewayId$TMPTBL;
    CREATE TEMP TABLE Transaction_ReadByGatewayId$TMPTBL
    AS
    SELECT
        *
        FROM transactionview
        WHERE gateway = par_Gateway AND LOWER(gatewayid) = LOWER(par_GatewayId);
END;
$procedure$
;
