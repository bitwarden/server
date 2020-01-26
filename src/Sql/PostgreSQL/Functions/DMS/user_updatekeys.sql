CREATE OR REPLACE PROCEDURE vault_dbo.user_updatekeys(par_id uuid, par_securitystamp character varying, par_key text, par_privatekey text, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo."User"
    SET securitystamp = par_SecurityStamp, key = par_Key, privatekey = par_PrivateKey, revisiondate = par_RevisionDate, accountrevisiondate = par_RevisionDate
        WHERE id = par_Id;
END;
$procedure$
;
