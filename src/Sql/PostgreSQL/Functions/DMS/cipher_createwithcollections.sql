CREATE OR REPLACE PROCEDURE cipher_createwithcollections(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_collection_ids guididarray)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_UpdateCollectionsSuccess NUMERIC(10, 0);
    CollectionIds GuidIdArray;
    cipher_updatecollections$refcur_1 refcursor;
    cipher_updatecollections$refcur_2 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL cipher_create(par_Id, par_UserId, par_OrganizationId, par_Type, par_Data, par_Favorites, par_Folders, par_Attachments, par_CreationDate, par_RevisionDate);
    PERFORM guididarray$aws$f('"par_CollectionIds$aws$tmp"');
    INSERT INTO "par_CollectionIds$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_CollectionIds);
    SELECT
        ARRAY_AGG(ROW ("par_CollectionIds$aws$tmp".*)::guididarray$aws$t ORDER BY oid)
        INTO STRICT CollectionIds
        FROM "par_CollectionIds$aws$tmp";
    CALL cipher_updatecollections(par_Id, par_UserId, par_OrganizationId, CollectionIds, return_code => var_UpdateCollectionsSuccess, p_refcur => cipher_updatecollections$refcur_1, p_refcur_2 => cipher_updatecollections$refcur_2);
END;
$procedure$
;
