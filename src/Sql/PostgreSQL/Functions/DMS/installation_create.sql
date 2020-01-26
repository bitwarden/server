CREATE OR REPLACE PROCEDURE vault_dbo.installation_create(par_id uuid, par_email character varying, par_key character varying, par_enabled numeric, par_creationdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo.installation (id, email, key, enabled, creationdate)
    VALUES (par_Id, par_Email, par_Key, aws_sqlserver_ext.tomsbit(par_Enabled), par_CreationDate);
END;
$procedure$
;
