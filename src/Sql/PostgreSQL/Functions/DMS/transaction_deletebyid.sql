CREATE OR REPLACE PROCEDURE transaction_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM transaction
        WHERE id = par_Id;
END;
$procedure$
;
