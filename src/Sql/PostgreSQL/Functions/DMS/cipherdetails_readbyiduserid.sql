CREATE OR REPLACE PROCEDURE cipherdetails_readbyiduserid(par_id uuid, par_userid uuid, INOUT p_refcur refcursor)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM usercipherdetails(par_UserId);
    OPEN p_refcur FOR
    SELECT
        *
        FROM usercipherdetails$tmptbl
        WHERE Id = par_Id
        ORDER BY Edit DESC NULLS FIRST
        LIMIT 1;
END;
$procedure$
;
