CREATE OR REPLACE PROCEDURE vault_dbo.user_bumpaccountrevisiondatebyorganizationuserid(par_organizationuserid uuid)
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
    INNER JOIN vault_dbo.organizationuser AS ou
        ON ou.userid = u.id
        WHERE ou.id = par_OrganizationUserId AND ou.status = 2
    /* Confirmed */
    ;
END;
$procedure$
;
