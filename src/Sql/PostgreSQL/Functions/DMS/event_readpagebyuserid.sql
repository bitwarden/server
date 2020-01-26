CREATE OR REPLACE PROCEDURE vault_dbo.event_readpagebyuserid(par_userid uuid, par_startdate timestamp without time zone, par_enddate timestamp without time zone, par_beforedate timestamp without time zone, par_pagesize numeric, INOUT p_refcur refcursor)
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
        FROM vault_dbo.eventview
        WHERE date >= par_StartDate AND (par_BeforeDate IS NOT NULL OR date <= par_EndDate) AND (par_BeforeDate IS NULL OR date < par_BeforeDate) AND organizationid IS NULL AND actinguserid = par_UserId
        ORDER BY date DESC NULLS FIRST
        OFFSET 0 LIMIT (par_PageSize);
END;
$procedure$
;
