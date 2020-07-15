CREATE OR REPLACE PROCEDURE organization_readabilities(INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        id, useevents, use2fa,
        CASE
            WHEN use2fa = 1 AND twofactorproviders IS NOT NULL AND LOWER(twofactorproviders) != LOWER('{}') THEN 1
            ELSE 0
        END AS using2fa, usersgetpremium, enabled
        FROM organization;
END;
$procedure$
;
