START TRANSACTION;

-- Drop the PasswordHealthReportApplications (plural) table
-- the correct table is PasswordHealthReportApplication (singular)
DROP TABLE IF EXISTS PasswordHealthReportApplications;

COMMIT;