/*
    Delete all secret version history - MSSQL

    Removes every row from [dbo].[SecretVersion] so version history starts fresh.
    Secrets themselves are untouched.

    Rows are deleted in batches and each batch commits on its own, so row locks are
    released between batches instead of escalating to a table lock that would block
    secret writes, and the transaction log can truncate as the delete progresses.
    This is the same DELETE TOP(@BatchSize) loop used by bulk deletes elsewhere in
    the repo, for example Event_DeleteManyByOrganizationId.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT COUNT(*) AS VersionRowsBeforeDelete FROM [dbo].[SecretVersion];

DECLARE @BatchSize INT = 1000;
DECLARE @Deleted   INT = 1;
DECLARE @Total     INT = 0;

WHILE @Deleted > 0
BEGIN
    DELETE TOP(@BatchSize)
    FROM [dbo].[SecretVersion];

    SET @Deleted = @@ROWCOUNT;
    SET @Total = @Total + @Deleted;
END

PRINT CONCAT('Deleted ', @Total, ' secret version row(s).');

SELECT COUNT(*) AS VersionRowsAfterDelete FROM [dbo].[SecretVersion];
