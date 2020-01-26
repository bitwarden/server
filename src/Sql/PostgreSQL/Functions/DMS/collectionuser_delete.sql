CREATE OR REPLACE PROCEDURE vault_dbo.collectionuser_delete(par_collectionid uuid, par_organizationuserid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo.collectionuser
        WHERE collectionid = par_CollectionId AND organizationuserid = par_OrganizationUserId;
    CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationuserid(par_OrganizationUserId);
END;
$procedure$
;
