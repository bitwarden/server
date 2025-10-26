-- Database name which is set from the backup-db.sh script.
DECLARE @DatabaseName varchar(100)
SET @DatabaseName = 'vault'

-- Database name without spaces for saving the backup files.
DECLARE @DatabaseNameSafe varchar(100)
SET @DatabaseNameSafe = 'vault'

DECLARE @BackupFile varchar(100)
SET @BackupFile = '$(BACKUP_DB_DIR)' + @DatabaseNameSafe + '_FULL_$(BACKUP_DB_FILENAME).BAK'

DECLARE @BackupName varchar(100)
SET @BackupName = @DatabaseName + ' full backup for $(BACKUP_DB_FILENAME)'

DECLARE @BackupCommand NVARCHAR(1000)
SET @BackupCommand = 'BACKUP DATABASE [' + @DatabaseName + '] TO DISK = ''' + @BackupFile + ''' WITH INIT, NAME= ''' + @BackupName + ''', NOSKIP, NOFORMAT'

EXEC(@BackupCommand)
