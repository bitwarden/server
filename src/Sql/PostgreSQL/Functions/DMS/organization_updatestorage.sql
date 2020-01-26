CREATE OR REPLACE PROCEDURE vault_dbo.organization_updatestorage(par_id uuid)
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
    --    CREATE TEMPORARY TABLE "#OrgStorageUpdateTemp"
    --    (id UUID NOT NULL,
    --        attachments TEXT NULL);
    --    INSERT INTO "#OrgStorageUpdateTemp"
    --    SELECT
    --        id, attachments
    --        FROM vault_dbo.cipher
    --        WHERE userid IS NULL AND organizationid = par_Id;
    --    WITH cte
    --    AS (SELECT
    --        id, (SELECT
    --            SUM(CAST (JSON_VALUE(value, '$.Size') AS NUMERIC(20, 0)))
    --            FROM
    --            /* Transformer error occurred */) AS size
    --        FROM "#OrgStorageUpdateTemp")
    --    SELECT
    --        SUM("[Size]")
    --        INTO var_Storage
    --        FROM cte;
    --    DROP TABLE "#OrgStorageUpdateTemp";
    --    UPDATE vault_dbo.organization
    --    SET storage = var_Storage, revisiondate = timezone('UTC', CURRENT_TIMESTAMP(6))
    --        WHERE id = par_Id;
    --    /*
    --
    --    DROP TABLE IF EXISTS "#OrgStorageUpdateTemp";
    --    */
    --    /*
    --
    --    Temporary table must be removed before end of the function.
    --    */
    -- END;
END;
$procedure$
;
