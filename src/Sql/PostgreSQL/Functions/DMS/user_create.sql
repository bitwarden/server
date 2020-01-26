CREATE OR REPLACE PROCEDURE vault_dbo.user_create(par_id uuid, par_name character varying, par_email character varying, par_emailverified numeric, par_masterpassword character varying, par_masterpasswordhint character varying, par_culture character varying, par_securitystamp character varying, par_twofactorproviders text, par_twofactorrecoverycode character varying, par_equivalentdomains text, par_excludedglobalequivalentdomains text, par_accountrevisiondate timestamp without time zone, par_key text, par_publickey text, par_privatekey text, par_premium numeric, par_premiumexpirationdate timestamp without time zone, par_renewalreminderdate timestamp without time zone, par_storage numeric, par_maxstoragegb numeric, par_gateway numeric, par_gatewaycustomerid character varying, par_gatewaysubscriptionid character varying, par_licensekey character varying, par_kdf numeric, par_kdfiterations numeric, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo."User" (id, name, email, emailverified, masterpassword, masterpasswordhint, culture, securitystamp, twofactorproviders, twofactorrecoverycode, equivalentdomains, excludedglobalequivalentdomains, accountrevisiondate, key, publickey, privatekey, premium, premiumexpirationdate, renewalreminderdate, storage, maxstoragegb, gateway, gatewaycustomerid, gatewaysubscriptionid, licensekey, kdf, kdfiterations, creationdate, revisiondate)
    VALUES (par_Id, par_Name, par_Email, aws_sqlserver_ext.tomsbit(par_EmailVerified), par_MasterPassword, par_MasterPasswordHint, par_Culture, par_SecurityStamp, par_TwoFactorProviders, par_TwoFactorRecoveryCode, par_EquivalentDomains, par_ExcludedGlobalEquivalentDomains, par_AccountRevisionDate, par_Key, par_PublicKey, par_PrivateKey, aws_sqlserver_ext.tomsbit(par_Premium), par_PremiumExpirationDate, par_RenewalReminderDate, par_Storage, par_MaxStorageGb, par_Gateway, par_GatewayCustomerId, par_GatewaySubscriptionId, par_LicenseKey, par_Kdf, par_KdfIterations, par_CreationDate, par_RevisionDate);
END;
$procedure$
;
