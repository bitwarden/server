CREATE OR REPLACE PROCEDURE vault_dbo.device_clearpushtokenbyid(par_id character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.device
    SET pushtoken = NULL
        WHERE id = par_Id::UUID;
END;
$procedure$
;
