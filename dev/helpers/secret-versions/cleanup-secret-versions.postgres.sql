/*
    Delete all secret version history - PostgreSQL

    Removes every row from "SecretVersion" so version history starts fresh.
    Secrets themselves are untouched. Runs in one transaction.
*/

BEGIN;

SELECT count(*) AS "VersionRowsBeforeDelete" FROM "SecretVersion";

DELETE FROM "SecretVersion";

SELECT count(*) AS "VersionRowsAfterDelete" FROM "SecretVersion";

COMMIT;
