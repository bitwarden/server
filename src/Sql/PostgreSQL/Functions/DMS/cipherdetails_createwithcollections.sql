CREATE OR REPLACE PROCEDURE cipherdetails_createwithcollections(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_folderid uuid, par_favorite numeric, par_edit numeric, par_organizationusetotp numeric, par_collection_ids guididarray)
 LANGUAGE plpgsql
AS $procedure$
/* not used */
/* not used */
/* not used */
/* not used */
/* not used */
DECLARE
    var_UpdateCollectionsSuccess NUMERIC(10, 0);
    cipher_updatecollections$refcur_1 refcursor;
    cipher_updatecollections$refcur_2 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL cipherdetails_create(par_Id, par_UserId, par_OrganizationId, par_Type, par_Data, par_Favorites, par_Folders, par_Attachments, par_CreationDate, par_RevisionDate, par_FolderId, par_Favorite, par_Edit, par_OrganizationUseTotp);
    CALL cipher_updatecollections(par_Id, par_UserId, par_OrganizationId, CollectionIds, return_code => var_UpdateCollectionsSuccess, p_refcur => cipher_updatecollections$refcur_1, p_refcur_2 => cipher_updatecollections$refcur_2);
END;
$procedure$
;
