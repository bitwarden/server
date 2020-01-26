CREATE OR REPLACE PROCEDURE vault_dbo.installation_update(par_id uuid, par_email character varying, par_key character varying, par_enabled numeric, par_creationdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.installation
    SET email = par_Email, key = par_Key, enabled = par_Enabled, creationdate = par_CreationDate
        WHERE id = par_Id;
END;
$procedure$
;
