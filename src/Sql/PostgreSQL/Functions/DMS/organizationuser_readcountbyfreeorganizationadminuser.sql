CREATE OR REPLACE PROCEDURE organization_user_readcountbyfreeorganizationadminuser(par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        COUNT(1)
        FROM organization_user AS ou
        INNER JOIN organization AS o
            ON o.id = ou.organizationid
        WHERE ou.userid = par_UserId AND ou.type < 2 AND
        /* Owner or Admin */
        o.plantype = 0 AND
        /* Free */
        ou.status = 2
    /* 2 = Confirmed */
    ;
END;
$procedure$
;
