CREATE OR REPLACE PROCEDURE group_createwithcollections(par_id uuid, par_organizationid uuid, par_name character varying, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_collections selectionreadonlyarray)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL group_create(par_Id, par_OrganizationId, par_Name, par_AccessAll, par_ExternalId, par_CreationDate, par_RevisionDate);
    PERFORM selectionreadonlyarray$aws$f('"par_Collections$aws$tmp"');
    INSERT INTO "par_Collections$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_Collections);
    WITH availablecollectionscte
    AS (SELECT
        id
        FROM collection
        WHERE organizationid = par_OrganizationId)
    INSERT INTO collection_group (collection_id, groupid, readonly)
    SELECT
        id, par_Id, readonly
        FROM "par_Collections$aws$tmp"
        WHERE id IN (SELECT
            id
            FROM availablecollectionscte);
    CALL user_bumpaccountrevisiondatebyorganizationid(par_OrganizationId);
END;
$procedure$
;
