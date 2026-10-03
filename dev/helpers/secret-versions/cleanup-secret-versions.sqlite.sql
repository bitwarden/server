/*
    Delete all secret version history - SQLite

    Removes every row from "SecretVersion" so version history starts fresh.
    Secrets themselves are untouched.

    The delete is not batched here, unlike the MSSQL, MySQL, and PostgreSQL scripts.
    SQLite has a single writer and no lock escalation or transaction log to manage,
    DELETE ... LIMIT is not available in default builds, and SQLite is only used for
    development and small self-hosted installs where the table is small.
*/

SELECT count(*) AS VersionRowsBeforeDelete FROM "SecretVersion";

DELETE FROM "SecretVersion";

SELECT count(*) AS VersionRowsAfterDelete FROM "SecretVersion";
