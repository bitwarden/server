CREATE OR REPLACE PROCEDURE vault_dbo.collection_createwithgroups(par_id uuid, par_organizationid uuid, par_name text, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_groups vault_dbo.selectionreadonlyarray)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.collection_create(par_Id, par_OrganizationId, par_Name, par_ExternalId, par_CreationDate, par_RevisionDate);
    PERFORM vault_dbo.selectionreadonlyarray$aws$f('"par_Groups$aws$tmp"');
    INSERT INTO "par_Groups$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_Groups);
    WITH availablegroupscte
    AS (SELECT
        id
        FROM vault_dbo."Group"
        WHERE organizationid = par_OrganizationId)
    INSERT INTO vault_dbo.collectiongroup (collectionid, groupid, readonly)
    SELECT
        par_Id, id, readonly
        FROM "par_Groups$aws$tmp"
        WHERE id IN (SELECT
            id
            FROM availablegroupscte);
    CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationid(par_OrganizationId);
END;
$procedure$
;
