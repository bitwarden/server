SET @run_migration_seats = 0;
SELECT 1 FROM `Organization`
WHERE `MaxAutoscaleSeats` IS NOT NULL
LIMIT 1
INTO @run_migration_seats;

SET @run_migration_email = 0;
SELECT 1 FROM `Organization`
WHERE `OwnersNotifiedOfAutoscaling` IS NOT NULL
LIMIT 1
INTO @run_migration_email;

SET @stmt = case @run_migration_seats + @run_migration_email
WHEN 0 THEN 'UPDATE `Organization` SET `MaxAutoscaleSeats` = `Seats`'
ELSE 'SELECT ''No migration necessary'''
END;

PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
