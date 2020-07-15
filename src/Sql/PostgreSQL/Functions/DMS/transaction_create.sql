CREATE OR REPLACE PROCEDURE transaction_create(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_amount numeric, par_refunded numeric, par_refundedamount numeric, par_details character varying, par_paymentmethodtype numeric, par_gateway numeric, par_gatewayid character varying, par_creationdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO transaction (id, userid, organizationid, type, amount, refunded, refundedamount, details, paymentmethodtype, gateway, gatewayid, creationdate)
    VALUES (par_Id, par_UserId, par_OrganizationId, par_Type, par_Amount, aws_sqlserver_ext.tomsbit(par_Refunded), par_RefundedAmount, par_Details, par_PaymentMethodType, par_Gateway, par_GatewayId, par_CreationDate);
END;
$procedure$
;
