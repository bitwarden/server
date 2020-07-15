CREATE OR REPLACE PROCEDURE device_create(par_id uuid, par_userid uuid, par_name character varying, par_type numeric, par_identifier character varying, par_pushtoken character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO device (id, userid, name, type, identifier, pushtoken, creationdate, revisiondate)
    VALUES (par_Id, par_UserId, par_Name, par_Type, par_Identifier, par_PushToken, par_CreationDate, par_RevisionDate);
END;
$procedure$
;
