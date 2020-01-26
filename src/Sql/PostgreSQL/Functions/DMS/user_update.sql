CREATE OR REPLACE PROCEDURE vault_dbo.user_update(par_id uuid, par_name character varying, par_email character varying, par_emailverified numeric, par_masterpassword character varying, par_masterpasswordhint character varying, par_culture character varying, par_securitystamp character varying, par_twofactorproviders text, par_twofactorrecoverycode character varying, par_equivalentdomains text, par_excludedglobalequivalentdomains text, par_accountrevisiondate timestamp without time zone, par_key text, par_publickey text, par_privatekey text, par_premium numeric, par_premiumexpirationdate timestamp without time zone, par_renewalreminderdate timestamp without time zone, par_storage numeric, par_maxstoragegb numeric, par_gateway numeric, par_gatewaycustomerid character varying, par_gatewaysubscriptionid character varying, par_licensekey character varying, par_kdf numeric, par_kdfiterations numeric, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo."User"
    SET name = par_Name, email = par_Email, emailverified = par_EmailVerified, masterpassword = par_MasterPassword, masterpasswordhint = par_MasterPasswordHint, culture = par_Culture, securitystamp = par_SecurityStamp, twofactorproviders = par_TwoFactorProviders, twofactorrecoverycode = par_TwoFactorRecoveryCode, equivalentdomains = par_EquivalentDomains, excludedglobalequivalentdomains = par_ExcludedGlobalEquivalentDomains, accountrevisiondate = par_AccountRevisionDate, key = par_Key, publickey = par_PublicKey, privatekey = par_PrivateKey, premium = par_Premium, premiumexpirationdate = par_PremiumExpirationDate, renewalreminderdate = par_RenewalReminderDate, storage = par_Storage, maxstoragegb = par_MaxStorageGb, gateway = par_Gateway, gatewaycustomerid = par_GatewayCustomerId, gatewaysubscriptionid = par_GatewaySubscriptionId, licensekey = par_LicenseKey, kdf = par_Kdf, kdfiterations = par_KdfIterations, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
END;
$procedure$
;
