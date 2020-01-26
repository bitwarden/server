CREATE OR REPLACE PROCEDURE vault_dbo."event_readpagebyorganizationidactinguserid$tmp"(par_organizationid uuid, par_actinguserid uuid, par_startdate timestamp without time zone, par_enddate timestamp without time zone, par_beforedate timestamp without time zone, par_pagesize numeric)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Event_ReadPageByOrganizationIdActingUserId$TMPTBL;
    CREATE TEMP TABLE Event_ReadPageByOrganizationIdActingUserId$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.eventview
        WHERE date >= par_StartDate AND (par_BeforeDate IS NOT NULL OR date <= par_EndDate) AND (par_BeforeDate IS NULL OR date < par_BeforeDate) AND organizationid = par_OrganizationId AND actinguserid = par_ActingUserId
        ORDER BY date DESC NULLS FIRST
        OFFSET 0 LIMIT (par_PageSize);
END;
$procedure$
;
