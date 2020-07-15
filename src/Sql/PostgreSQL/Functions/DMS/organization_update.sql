CREATE OR REPLACE PROCEDURE organization_update(par_id uuid, par_name character varying, par_businessname character varying, par_businessaddress1 character varying, par_businessaddress2 character varying, par_businessaddress3 character varying, par_businesscountry character varying, par_businesstaxnumber character varying, par_billingemail character varying, par_plan character varying, par_plantype numeric, par_seats numeric, par_maxcollections numeric, par_usegroups numeric, par_usedirectory numeric, par_useevents numeric, par_usetotp numeric, par_use2fa numeric, par_useapi numeric, par_selfhost numeric, par_usersgetpremium numeric, par_storage numeric, par_maxstoragegb numeric, par_gateway numeric, par_gatewaycustomerid character varying, par_gatewaysubscriptionid character varying, par_enabled numeric, par_licensekey character varying, par_apikey character varying, par_twofactorproviders text, par_expirationdate timestamp without time zone, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE organization
    SET name = par_Name, businessname = par_BusinessName, businessaddress1 = par_BusinessAddress1, businessaddress2 = par_BusinessAddress2, businessaddress3 = par_BusinessAddress3, businesscountry = par_BusinessCountry, businesstaxnumber = par_BusinessTaxNumber, billingemail = par_BillingEmail, plan = par_Plan, plantype = par_PlanType, seats = par_Seats, maxcollections = par_MaxCollections, usegroups = par_UseGroups, usedirectory = par_UseDirectory, useevents = par_UseEvents, usetotp = par_UseTotp, use2fa = par_Use2fa, useapi = par_UseApi, selfhost = par_SelfHost, usersgetpremium = par_UsersGetPremium, storage = par_Storage, maxstoragegb = par_MaxStorageGb, gateway = par_Gateway, gatewaycustomerid = par_GatewayCustomerId, gatewaysubscriptionid = par_GatewaySubscriptionId, enabled = par_Enabled, licensekey = par_LicenseKey, apikey = par_ApiKey, twofactorproviders = par_TwoFactorProviders, expirationdate = par_ExpirationDate, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
END;
$procedure$
;
