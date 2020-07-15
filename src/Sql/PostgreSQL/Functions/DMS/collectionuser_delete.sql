CREATE OR REPLACE PROCEDURE collectionuser_delete(par_collection_id uuid, par_organization_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM collectionuser
        WHERE collection_id = par_CollectionId AND organization_userid = par_OrganizationUserId;
    CALL user_bumpaccountrevisiondatebyorganization_userid(par_OrganizationUserId);
END;
$procedure$
;
