CREATE OR REPLACE PROCEDURE vault_dbo.u2f_create(par_id numeric, par_userid uuid, par_keyhandle character varying, par_challenge character varying, par_appid character varying, par_version character varying, par_creationdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo.u2f (userid, keyhandle, challenge, appid, version, creationdate)
    VALUES (par_UserId, par_KeyHandle, par_Challenge, par_AppId, par_Version, par_CreationDate);
END;
$procedure$
;
