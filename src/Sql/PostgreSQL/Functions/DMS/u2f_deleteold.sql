CREATE OR REPLACE PROCEDURE u2f_deleteold()
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
    var_Threshold TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT timezone('UTC', CURRENT_TIMESTAMP(6)) + (- 7::NUMERIC || ' DAY')::INTERVAL;
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
                    [dbo].[U2f]
                WHERE
                    [CreationDate] IS NULL
                    OR [CreationDate] < @Threshold
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
    END LOOP;
END;
$procedure$
;
