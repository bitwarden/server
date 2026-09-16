/*
    Delete all secret version history - MySQL / MariaDB

    Removes every row from `SecretVersion` so version history starts fresh.
    Secrets themselves are untouched.

    Rows are deleted in batches and each batch commits on its own, so InnoDB's undo
    log stays small and row locks are released between batches instead of being
    held for the whole table. Looping needs a stored procedure; it is created,
    called, and dropped in this script.

    Uses the mysql client's DELIMITER command, so run it through the mysql client
    (or MariaDB's equivalent) rather than a driver that sends the file as one statement.
*/

SELECT COUNT(*) AS VersionRowsBeforeDelete FROM `SecretVersion`;

DROP PROCEDURE IF EXISTS `DeleteSecretVersionsInBatches`;

DELIMITER $$
CREATE PROCEDURE `DeleteSecretVersionsInBatches`(IN batch_size INT)
BEGIN
    DECLARE deleted INT DEFAULT 1;
    DECLARE total BIGINT DEFAULT 0;

    WHILE deleted > 0 DO
        DELETE FROM `SecretVersion` LIMIT batch_size;
        SET deleted = ROW_COUNT();
        SET total = total + deleted;
        COMMIT;
    END WHILE;

    SELECT total AS DeletedRows;
END$$
DELIMITER ;

CALL `DeleteSecretVersionsInBatches`(1000);

DROP PROCEDURE IF EXISTS `DeleteSecretVersionsInBatches`;

SELECT COUNT(*) AS VersionRowsAfterDelete FROM `SecretVersion`;
