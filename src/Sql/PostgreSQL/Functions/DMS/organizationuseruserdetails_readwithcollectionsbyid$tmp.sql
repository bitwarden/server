CREATE OR REPLACE PROCEDURE "organization_useruserdetails_readwithcollectionsbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    organization_useruserdetails_readbyid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL organization_useruserdetails_readbyid(par_Id, p_refcur => organization_useruserdetails_readbyid$refcur_1);
    DROP TABLE IF EXISTS OrganizationUserUserDetails_ReadWithCollectionsById$TMPTBL;
    CREATE TEMP TABLE OrganizationUserUserDetails_ReadWithCollectionsById$TMPTBL
    AS
    SELECT
        cu.collection_id AS id, cu.readonly
        FROM organization_user AS ou
        INNER JOIN collectionuser AS cu
            ON ou.accessall = 0 AND cu.organization_userid = ou.id
        WHERE organization_userid = par_Id;
END;
$procedure$
;
