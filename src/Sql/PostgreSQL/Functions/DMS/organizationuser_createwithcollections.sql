CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_createwithcollections(par_id uuid, par_organizationid uuid, par_userid uuid, par_email character varying, par_key text, par_status numeric, par_type numeric, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_collections vault_dbo.selectionreadonlyarray)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.organizationuser_create(par_Id, par_OrganizationId, par_UserId, par_Email, par_Key, par_Status, par_Type, par_AccessAll, par_ExternalId, par_CreationDate, par_RevisionDate);
    WITH availablecollectionscte
    AS (SELECT
        id
        FROM vault_dbo.collection
        WHERE organizationid = par_OrganizationId)
    INSERT INTO vault_dbo.collectionuser (collectionid, organizationuserid, readonly)
    SELECT
        id, par_Id, readonly
        FROM "par_Collections$aws$tmp"
        WHERE id IN (SELECT
            id
            FROM availablecollectionscte);
END;
$procedure$
;
