/*
    Delete all secret version history - PostgreSQL

    Removes every row from "SecretVersion" so version history starts fresh.
    Secrets themselves are untouched.

    Rows are deleted in batches and each batch commits on its own, so locks are
    released between batches and autovacuum can reclaim dead tuples as the delete
    progresses instead of facing a whole table's worth at once. Committing inside a
    loop needs a procedure (PostgreSQL 11+). It is created in the session-local
    pg_temp schema and dropped again at the end.

    Run with autocommit on, which is the psql default. CALL cannot commit when it is
    wrapped in an outer BEGIN block.
*/

SELECT count(*) AS "VersionRowsBeforeDelete" FROM "SecretVersion";

CREATE OR REPLACE PROCEDURE pg_temp.delete_secret_versions_in_batches(batch_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
    deleted bigint;
    total   bigint := 0;
BEGIN
    LOOP
        DELETE FROM "SecretVersion"
        WHERE "Id" IN (SELECT "Id" FROM "SecretVersion" LIMIT batch_size);

        GET DIAGNOSTICS deleted = ROW_COUNT;
        total := total + deleted;
        COMMIT;

        EXIT WHEN deleted = 0;
    END LOOP;

    RAISE NOTICE 'Deleted % secret version row(s).', total;
END
$$;

CALL pg_temp.delete_secret_versions_in_batches(1000);

DROP PROCEDURE pg_temp.delete_secret_versions_in_batches(integer);

SELECT count(*) AS "VersionRowsAfterDelete" FROM "SecretVersion";
