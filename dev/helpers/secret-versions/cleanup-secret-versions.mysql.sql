/*
    Delete all secret version history - MySQL / MariaDB

    Removes every row from `SecretVersion` so version history starts fresh.
    Secrets themselves are untouched. Runs in one transaction.
*/

START TRANSACTION;

SELECT COUNT(*) AS VersionRowsBeforeDelete FROM `SecretVersion`;

DELETE FROM `SecretVersion`;

SELECT COUNT(*) AS VersionRowsAfterDelete FROM `SecretVersion`;

COMMIT;
