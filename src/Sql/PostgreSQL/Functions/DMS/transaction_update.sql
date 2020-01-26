CREATE OR REPLACE PROCEDURE vault_dbo.transaction_update(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_amount numeric, par_refunded numeric, par_refundedamount numeric, par_details character varying, par_paymentmethodtype numeric, par_gateway numeric, par_gatewayid character varying, par_creationdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.transaction
    SET userid = par_UserId, organizationid = par_OrganizationId, type = par_Type, amount = par_Amount, refunded = par_Refunded, refundedamount = par_RefundedAmount, details = par_Details, paymentmethodtype = par_PaymentMethodType, gateway = par_Gateway, gatewayid = par_GatewayId, creationdate = par_CreationDate
        WHERE id = par_Id;
END;
$procedure$
;
