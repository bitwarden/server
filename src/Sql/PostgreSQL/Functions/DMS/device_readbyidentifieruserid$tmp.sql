CREATE OR REPLACE PROCEDURE "device_readbyidentifieruserid$tmp"(par_userid uuid, par_identifier character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Device_ReadByIdentifierUserId$TMPTBL;
    CREATE TEMP TABLE Device_ReadByIdentifierUserId$TMPTBL
    AS
    SELECT
        *
        FROM deviceview
        WHERE userid = par_UserId AND LOWER(identifier) = LOWER(par_Identifier);
END;
$procedure$
;
