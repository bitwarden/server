CREATE OR REPLACE PROCEDURE organization_create(par_id uuid, par_name character varying, par_businessname character varying, par_businessaddress1 character varying, par_businessaddress2 character varying, par_businessaddress3 character varying, par_businesscountry character varying, par_businesstaxnumber character varying, par_billingemail character varying, par_plan character varying, par_plantype numeric, par_seats numeric, par_maxcollections numeric, par_usegroups numeric, par_usedirectory numeric, par_useevents numeric, par_usetotp numeric, par_use2fa numeric, par_useapi numeric, par_selfhost numeric, par_usersgetpremium numeric, par_storage numeric, par_maxstoragegb numeric, par_gateway numeric, par_gatewaycustomerid character varying, par_gatewaysubscriptionid character varying, par_enabled numeric, par_licensekey character varying, par_apikey character varying, par_twofactorproviders text, par_expirationdate timestamp without time zone, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO organization (id, name, businessname, businessaddress1, businessaddress2, businessaddress3, businesscountry, businesstaxnumber, billingemail, plan, plantype, seats, maxcollections, usegroups, usedirectory, useevents, usetotp, use2fa, useapi, selfhost, usersgetpremium, storage, maxstoragegb, gateway, gatewaycustomerid, gatewaysubscriptionid, enabled, licensekey, apikey, twofactorproviders, expirationdate, creationdate, revisiondate)
    VALUES (par_Id, par_Name, par_BusinessName, par_BusinessAddress1, par_BusinessAddress2, par_BusinessAddress3, par_BusinessCountry, par_BusinessTaxNumber, par_BillingEmail, par_Plan, par_PlanType, par_Seats, par_MaxCollections, aws_sqlserver_ext.tomsbit(par_UseGroups), aws_sqlserver_ext.tomsbit(par_UseDirectory), aws_sqlserver_ext.tomsbit(par_UseEvents), aws_sqlserver_ext.tomsbit(par_UseTotp), aws_sqlserver_ext.tomsbit(par_Use2fa), aws_sqlserver_ext.tomsbit(par_UseApi), aws_sqlserver_ext.tomsbit(par_SelfHost), aws_sqlserver_ext.tomsbit(par_UsersGetPremium), par_Storage, par_MaxStorageGb, par_Gateway, par_GatewayCustomerId, par_GatewaySubscriptionId, aws_sqlserver_ext.tomsbit(par_Enabled), par_LicenseKey, par_ApiKey, par_TwoFactorProviders, par_ExpirationDate, par_CreationDate, par_RevisionDate);
END;
$procedure$
;
