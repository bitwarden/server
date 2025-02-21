-- Database name which is set from the backup-db.sh script.
DECLARE @DatabaseName varchar(100)
SET @DatabaseName = 'vault'

-- Database name without spaces for saving the backup files.
DECLARE @DatabaseNameSafe varchar(100)
SET @DatabaseNameSafe = 'vault'

DECLARE @LogFile varchar(100)
SET @LogFile = '/etc/bitwarden/mssql/backups/' + @DatabaseNameSafe + '_TLOG_$(now).TRN'
DECLARE @BackupFile varchar(100)
SET @BackupFile = '/etc/bitwarden/mssql/backups/' + @DatabaseNameSafe + '_FULL_$(now).BAK'

DECLARE @LogName varchar(100)
SET @LogName = @DatabaseName + ' tlog backup for $(now)'
DECLARE @BackupName varchar(100)
SET @BackupName = @DatabaseName + ' full backup for $(now)'

DECLARE @LogCommand NVARCHAR(1000)
SET @LogCommand = 'BACKUP LOG [' + @DatabaseName + '] TO DISK = ''' + @LogFile + ''' WITH INIT, NAME= ''' + @LogName + ''', NOSKIP, NOFORMAT'
DECLARE @BackupCommand NVARCHAR(1000)
SET @BackupCommand = 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + @BackupFile + ''' WITH INIT, NAME= ''' + @BackupName + ''', NOSKIP, NOFORMAT'

EXEC(@LogCommand)
EXEC(@BackupCommand)
