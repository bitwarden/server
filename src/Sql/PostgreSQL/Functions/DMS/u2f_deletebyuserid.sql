CREATE OR REPLACE PROCEDURE u2f_deletebyuserid(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM u2f
        WHERE userid = par_UserId;
END;
$procedure$
;
