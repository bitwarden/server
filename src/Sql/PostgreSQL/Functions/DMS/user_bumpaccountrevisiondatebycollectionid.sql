CREATE OR REPLACE PROCEDURE vault_dbo.user_bumpaccountrevisiondatebycollectionid(par_collectionid uuid, par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo."User"
    SET accountrevisiondate = timezone('UTC', CURRENT_TIMESTAMP(6))
    FROM vault_dbo."User" AS u
    LEFT OUTER JOIN vault_dbo.organizationuser AS ou
        ON ou.userid = u.id
    LEFT OUTER JOIN vault_dbo.collectionuser AS cu
        ON ou.accessall = 0 AND cu.organizationuserid = ou.id AND cu.collectionid = par_CollectionId
    LEFT OUTER JOIN vault_dbo.groupuser AS gu
        ON cu.collectionid IS NULL AND ou.accessall = 0 AND gu.organizationuserid = ou.id
    LEFT OUTER JOIN vault_dbo."Group" AS g
        ON g.id = gu.groupid
    LEFT OUTER JOIN vault_dbo.collectiongroup AS cg
        ON g.accessall = 0 AND cg.groupid = gu.groupid AND cg.collectionid = par_CollectionId
        WHERE ou.status = 2 AND
        /* 2 = Confirmed */
        (cu.collectionid IS NOT NULL OR cg.collectionid IS NOT NULL OR (ou.organizationid = par_OrganizationId AND (ou.accessall = 1 OR g.accessall = 1)));
END;
$procedure$
;
