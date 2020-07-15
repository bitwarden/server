CREATE OR REPLACE PROCEDURE user_bumpaccountrevisiondatebyorganizationid(par_organizationid uuid)
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
    INNER JOIN organization_user AS ou
        ON ou.userid = u.id
        WHERE ou.organizationid = par_OrganizationId AND ou.status = 2
    /* Confirmed */
    ;
END;
$procedure$
;
