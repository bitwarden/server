CREATE OR REPLACE PROCEDURE organization_userorganizationdetails_readbyuseridstatus(par_userid uuid, par_status numeric, INOUT p_refcur refcursor)
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
        FROM organization_userorganizationdetailsview
        WHERE userid = par_UserId AND (par_Status IS NULL OR status = par_Status);
END;
$procedure$
;
