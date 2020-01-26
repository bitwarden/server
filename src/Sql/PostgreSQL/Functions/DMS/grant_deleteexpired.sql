CREATE OR REPLACE PROCEDURE vault_dbo.grant_deleteexpired()
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
    var_Now TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT timezone('UTC', CURRENT_TIMESTAMP(6));
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    WHILE var_BatchSize > 0 LOOP
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Grant]
                WHERE
                    [ExpirationDate] < @Now
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
    END LOOP;
END;
$procedure$
;
