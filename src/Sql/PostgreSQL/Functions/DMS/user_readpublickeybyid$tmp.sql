CREATE OR REPLACE PROCEDURE "user_readpublickeybyid$tmp"(par_id character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS User_ReadPublicKeyById$TMPTBL;
    CREATE TEMP TABLE User_ReadPublicKeyById$TMPTBL
    AS
    SELECT
        publickey
        FROM "User"
        WHERE id = par_Id::UUID;
END;
$procedure$
;
