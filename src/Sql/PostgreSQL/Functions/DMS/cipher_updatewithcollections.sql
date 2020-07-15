CREATE OR REPLACE PROCEDURE cipher_updatewithcollections(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_collection_ids guididarray, INOUT p_refcur refcursor, INOUT p_refcur_2 refcursor)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_UpdateCollectionsSuccess NUMERIC(10, 0);
    cipher_updatecollections$refcur_1 refcursor;
    cipher_updatecollections$refcur_2 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    /*
    [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
    BEGIN TRANSACTION Cipher_UpdateWithCollections
    */
    CALL cipher_updatecollections(par_Id, par_UserId, par_OrganizationId, CollectionIds, return_code => var_UpdateCollectionsSuccess, p_refcur => cipher_updatecollections$refcur_1, p_refcur_2 => cipher_updatecollections$refcur_2);

    IF var_UpdateCollectionsSuccess < 0 THEN
        COMMIT;
        OPEN p_refcur FOR
        SELECT
            - 1;
        /* -1 = Failure */
        RETURN;
    END IF;
    UPDATE cipher
    SET userid = NULL, organizationid = par_OrganizationId, data = par_Data, attachments = par_Attachments, revisiondate = par_RevisionDate
        /* No need to update CreationDate, Favorites, Folders, or Type since that data will not change */
        WHERE id = par_Id;
    COMMIT;

    IF par_Attachments IS NOT NULL THEN
        CALL organization_updatestorage(par_OrganizationId);
        CALL user_updatestorage(par_UserId);
    END IF;
    CALL user_bumpaccountrevisiondatebycipherid(par_Id, par_OrganizationId);
    OPEN p_refcur_2 FOR
    SELECT
        0
    /* 0 = Success */
    ;
END;
$procedure$
;
