/*
    Delete all secret version history - MSSQL

    Removes every row from [dbo].[SecretVersion] so version history starts fresh.
    Secrets themselves are untouched. Runs in one transaction.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

SELECT COUNT(*) AS VersionRowsBeforeDelete FROM [dbo].[SecretVersion];

DELETE FROM [dbo].[SecretVersion];

SELECT COUNT(*) AS VersionRowsAfterDelete FROM [dbo].[SecretVersion];

COMMIT TRANSACTION;
