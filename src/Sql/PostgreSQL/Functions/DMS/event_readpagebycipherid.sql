CREATE OR REPLACE PROCEDURE vault_dbo.event_readpagebycipherid(par_organizationid uuid, par_userid uuid, par_cipherid uuid, par_startdate timestamp without time zone, par_enddate timestamp without time zone, par_beforedate timestamp without time zone, par_pagesize numeric, INOUT p_refcur refcursor)
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
        WHERE date >= par_StartDate AND (par_BeforeDate IS NOT NULL OR date <= par_EndDate) AND (par_BeforeDate IS NULL OR date < par_BeforeDate) AND ((par_OrganizationId IS NULL AND organizationid IS NULL) OR (par_OrganizationId IS NOT NULL AND organizationid = par_OrganizationId)) AND ((par_UserId IS NULL AND userid IS NULL) OR (par_UserId IS NOT NULL AND userid = par_UserId)) AND cipherid = par_CipherId
        ORDER BY date DESC NULLS FIRST
        OFFSET 0 LIMIT (par_PageSize);
END;
$procedure$
;
