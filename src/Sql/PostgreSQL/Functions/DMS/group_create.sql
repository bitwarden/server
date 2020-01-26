CREATE OR REPLACE PROCEDURE vault_dbo.group_create(par_id uuid, par_organizationid uuid, par_name character varying, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo."Group" (id, organizationid, name, accessall, externalid, creationdate, revisiondate)
    VALUES (par_Id, par_OrganizationId, par_Name, aws_sqlserver_ext.tomsbit(par_AccessAll), par_ExternalId, par_CreationDate, par_RevisionDate);
END;
$procedure$
;
