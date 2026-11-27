-- Sets the database options that the cloud vault databases run with, so local and CI databases lock the same way.
-- Self-hosted databases keep the SQL Server defaults. Usage: sqlcmd -v DatabaseName=vault_dev -i cloud_database_options.sql
ALTER DATABASE [$(DatabaseName)] SET ACCELERATED_DATABASE_RECOVERY = ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$(DatabaseName)] SET OPTIMIZED_LOCKING = ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$(DatabaseName)] SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

SELECT
    [name],
    [is_accelerated_database_recovery_on],
    [is_optimized_locking_on],
    [snapshot_isolation_state_desc],
    [is_read_committed_snapshot_on]
FROM
    sys.databases
WHERE
    [name] = N'$(DatabaseName)';
GO
