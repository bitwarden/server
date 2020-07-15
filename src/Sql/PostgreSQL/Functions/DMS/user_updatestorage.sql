CREATE OR REPLACE PROCEDURE user_updatestorage(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
-- Converted with error!
    -- DECLARE
    --    var_Storage NUMERIC(20, 0);
    -- BEGIN
    --    /*
    --    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    --    SET NOCOUNT ON
    --    */
    --    CREATE TEMPORARY TABLE "#UserStorageUpdateTemp"
    --    (id UUID NOT NULL,
    --        attachments TEXT NULL);
    --    INSERT INTO "#UserStorageUpdateTemp"
    --    SELECT
    --        id, attachments
    --        FROM cipher
    --        WHERE userid = par_Id;
    --    WITH cte
    --    AS (SELECT
    --        id, (SELECT
    --            SUM(CAST (JSON_VALUE(value, '$.Size') AS NUMERIC(20, 0)))
    --            FROM
    --            /* Transformer error occurred */) AS size
    --        FROM "#UserStorageUpdateTemp")
    --    SELECT
    --        SUM(cte.size)
    --        INTO var_Storage
    --        FROM cte;
    --    DROP TABLE "#UserStorageUpdateTemp";
    --    UPDATE "User"
    --    SET storage = var_Storage, revisiondate = timezone('UTC', CURRENT_TIMESTAMP(6))
    --        WHERE id = par_Id;
    --    /*
    --
    --    DROP TABLE IF EXISTS "#UserStorageUpdateTemp";
    --    */
    --    /*
    --
    --    Temporary table must be removed before end of the function.
    --    */
    -- END;
END;
$procedure$
;
