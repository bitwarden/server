CREATE OR REPLACE PROCEDURE user_readbyemail(par_email character varying, INOUT p_refcur refcursor)
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
        FROM userview
        WHERE LOWER(email) = LOWER(par_Email);
END;
$procedure$
;
