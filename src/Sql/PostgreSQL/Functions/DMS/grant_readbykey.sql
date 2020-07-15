CREATE OR REPLACE PROCEDURE grant_readbykey(par_key character varying, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        *
        FROM grantview
        WHERE LOWER(key) = LOWER(par_Key);
END;
$procedure$
;
