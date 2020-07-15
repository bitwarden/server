CREATE OR REPLACE PROCEDURE organization_user_create(par_id uuid, par_organizationid uuid, par_userid uuid, par_email character varying, par_key text, par_status numeric, par_type numeric, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO organization_user (id, organizationid, userid, email, key, status, type, accessall, externalid, creationdate, revisiondate)
    VALUES (par_Id, par_OrganizationId, par_UserId, par_Email, par_Key, par_Status, par_Type, aws_sqlserver_ext.tomsbit(par_AccessAll), par_ExternalId, par_CreationDate, par_RevisionDate);
END;
$procedure$
;
