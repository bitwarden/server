CREATE OR REPLACE PROCEDURE user_search(par_email character varying, par_skip numeric DEFAULT 0, par_take numeric DEFAULT 25, INOUT p_refcur refcursor DEFAULT NULL::refcursor)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_EmailLikeSearch VARCHAR(55) DEFAULT par_Email || '%';
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    OPEN p_refcur FOR
    SELECT
        *
        FROM userview
        WHERE (par_Email IS NULL OR LOWER(email) LIKE LOWER(var_EmailLikeSearch))
        ORDER BY email ASC NULLS FIRST
        OFFSET (par_Skip) LIMIT (par_Take);
END;
$procedure$
;
