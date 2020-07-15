CREATE OR REPLACE PROCEDURE collection_create(par_id uuid, par_organizationid uuid, par_name text, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO collection (id, organizationid, name, externalid, creationdate, revisiondate)
    VALUES (par_Id, par_OrganizationId, par_Name, par_ExternalId, par_CreationDate, par_RevisionDate);
    CALL user_bumpaccountrevisiondatebycollection_id(par_Id, par_OrganizationId);
END;
$procedure$
;
