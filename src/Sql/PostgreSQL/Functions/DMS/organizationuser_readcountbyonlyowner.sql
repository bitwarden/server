CREATE OR REPLACE PROCEDURE organization_user_readcountbyonlyowner(par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON

        ;
    */
    OPEN p_refcur FOR
    WITH ownercountcte
    AS (SELECT
        ou.userid, COUNT(1) OVER (PARTITION BY ou.organizationid) AS confirmedownercount
        FROM organization_user AS ou
        WHERE ou.type = 0 AND
        /* 0 = Owner */
        ou.status = 2
    /* 2 = Confirmed */
    )
    SELECT
        COUNT(1)
        FROM ownercountcte AS oc
        WHERE oc.userid = par_UserId AND oc."[ConfirmedOwnerCount]" = 1;
END;
$procedure$
;
