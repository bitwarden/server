CREATE OR REPLACE PROCEDURE vault_dbo.collection_update(par_id uuid, par_organizationid uuid, par_name text, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.collection
    SET organizationid = par_OrganizationId, name = par_Name, externalid = par_ExternalId, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
    CALL vault_dbo.user_bumpaccountrevisiondatebycollectionid(par_Id, par_OrganizationId);
END;
$procedure$
;
