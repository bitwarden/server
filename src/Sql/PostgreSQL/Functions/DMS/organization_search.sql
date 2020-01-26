CREATE OR REPLACE PROCEDURE vault_dbo.organization_search(par_name character varying, par_useremail character varying, par_paid numeric, par_skip numeric DEFAULT 0, par_take numeric DEFAULT 25, INOUT p_refcur refcursor DEFAULT NULL::refcursor, INOUT p_refcur_2 refcursor DEFAULT NULL::refcursor)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_NameLikeSearch VARCHAR(55) DEFAULT '%' || par_Name || '%';
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    IF par_UserEmail IS NOT NULL THEN
        OPEN p_refcur FOR
        SELECT
            o.*
            FROM vault_dbo.organizationview AS o
            INNER JOIN vault_dbo.organizationuser AS ou
                ON o.id = ou.organizationid
            INNER JOIN vault_dbo."User" AS u
                ON u.id = ou.userid
            WHERE (par_Name IS NULL OR LOWER(o.name) LIKE LOWER(var_NameLikeSearch)) AND (par_UserEmail IS NULL OR LOWER(u.email) = LOWER(par_UserEmail)) AND (par_Paid IS NULL OR ((par_Paid = 1 AND o.gatewaysubscriptionid IS NOT NULL) OR (par_Paid = 0 AND o.gatewaysubscriptionid IS NULL)))
            ORDER BY o.creationdate DESC NULLS FIRST
            OFFSET (par_Skip) LIMIT (par_Take);
    ELSE
        OPEN p_refcur_2 FOR
        SELECT
            o.*
            FROM vault_dbo.organizationview AS o
            WHERE (par_Name IS NULL OR LOWER(o.name) LIKE LOWER(var_NameLikeSearch)) AND (par_Paid IS NULL OR ((par_Paid = 1 AND o.gatewaysubscriptionid IS NOT NULL) OR (par_Paid = 0 AND o.gatewaysubscriptionid IS NULL)))
            ORDER BY o.creationdate DESC NULLS FIRST
            OFFSET (par_Skip) LIMIT (par_Take);
    END IF;
END;
$procedure$
;
