CREATE OR REPLACE PROCEDURE "device_readbyidentifier$tmp"(par_identifier character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Device_ReadByIdentifier$TMPTBL;
    CREATE TEMP TABLE Device_ReadByIdentifier$TMPTBL
    AS
    SELECT
        *
        FROM deviceview
        WHERE LOWER(identifier) = LOWER(par_Identifier);
END;
$procedure$
;
