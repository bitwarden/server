CREATE OR REPLACE PROCEDURE vault_dbo."organizationuseruserdetails_readwithcollectionsbyid$tmp"(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    organizationuseruserdetails_readbyid$refcur_1 refcursor;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL vault_dbo.organizationuseruserdetails_readbyid(par_Id, p_refcur => organizationuseruserdetails_readbyid$refcur_1);
    DROP TABLE IF EXISTS OrganizationUserUserDetails_ReadWithCollectionsById$TMPTBL;
    CREATE TEMP TABLE OrganizationUserUserDetails_ReadWithCollectionsById$TMPTBL
    AS
    SELECT
        cu.collectionid AS id, cu.readonly
        FROM vault_dbo.organizationuser AS ou
        INNER JOIN vault_dbo.collectionuser AS cu
            ON ou.accessall = 0 AND cu.organizationuserid = ou.id
        WHERE organizationuserid = par_Id;
END;
$procedure$
;
