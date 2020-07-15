CREATE OR REPLACE PROCEDURE user_bumpaccountrevisiondatebycollection_id(par_collection_id uuid, par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE "User"
    SET accountrevisiondate = timezone('UTC', CURRENT_TIMESTAMP(6))
    FROM "User" AS u
    LEFT OUTER JOIN organization_user AS ou
        ON ou.userid = u.id
    LEFT OUTER JOIN collectionuser AS cu
        ON ou.accessall = 0 AND cu.organization_userid = ou.id AND cu.collection_id = par_CollectionId
    LEFT OUTER JOIN groupuser AS gu
        ON cu.collection_id IS NULL AND ou.accessall = 0 AND gu.organization_userid = ou.id
    LEFT OUTER JOIN "Group" AS g
        ON g.id = gu.groupid
    LEFT OUTER JOIN collection_group AS cg
        ON g.accessall = 0 AND cg.groupid = gu.groupid AND cg.collection_id = par_CollectionId
        WHERE ou.status = 2 AND
        /* 2 = Confirmed */
        (cu.collection_id IS NOT NULL OR cg.collection_id IS NOT NULL OR (ou.organizationid = par_OrganizationId AND (ou.accessall = 1 OR g.accessall = 1)));
END;
$procedure$
;
