CREATE OR REPLACE PROCEDURE organization_readbyuserid(par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        o.*
        FROM organizationview AS o
        INNER JOIN organization_user AS ou
            ON o.id = ou.organizationid
        WHERE ou.userid = par_UserId;
END;
$procedure$
;
