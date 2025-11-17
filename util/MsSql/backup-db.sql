-- Database name which is set from the backup-db.sh script.
DECLARE @DatabaseName varchar(100)
SET @DatabaseName = 'vault'

-- Check if database is in FULL recovery and has never had a t-log backup
IF EXISTS (
    SELECT 1 FROM sys.databases
    WHERE name = @DatabaseName AND recovery_model = 1  -- 1 = FULL
) AND NOT EXISTS (
    SELECT 1 FROM msdb.dbo.backupset
    WHERE database_name = @DatabaseName AND type = 'L'  -- L = Transaction Log
)
BEGIN   
    EXEC('ALTER DATABASE [' + @DatabaseName + '] SET RECOVERY SIMPLE')
END

-- Database name without spaces for saving the backup files.
DECLARE @DatabaseNameSafe varchar(100)
SET @DatabaseNameSafe = 'vault'

DECLARE @BackupFile varchar(100)
SET @BackupFile = '/etc/bitwarden/mssql/backups/' + @DatabaseNameSafe + '_FULL_$(now).BAK'

DECLARE @BackupName varchar(100)
SET @BackupName = @DatabaseName + ' full backup for $(now)'

DECLARE @BackupCommand NVARCHAR(1000)
SET @BackupCommand = 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + @BackupFile + ''' WITH INIT, NAME= ''' + @BackupName + ''', NOSKIP, NOFORMAT'

EXEC(@BackupCommand)
