CREATE OR REPLACE PROCEDURE vault_dbo.device_update(par_id uuid, par_userid uuid, par_name character varying, par_type numeric, par_identifier character varying, par_pushtoken character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.device
    SET userid = par_UserId, name = par_Name, type = par_Type, identifier = par_Identifier, pushtoken = par_PushToken, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
END;
$procedure$
;
