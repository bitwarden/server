DECLARE @BackupFile varchar(100)
SET @BackupFile = '/etc/bitwarden/mssql/backups/vault_FULL_$(now).BAK'

DECLARE @BackupName varchar(100)
SET @BackupName = 'vault full backup for $(now)'

DECLARE @BackupCommand NVARCHAR(1000)
SET @BackupCommand = 'BACKUP DATABASE [vault] TO DISK = ''' + @BackupFile + ''' WITH INIT, NAME= ''' + @BackupName + ''', NOSKIP, NOFORMAT'

EXEC(@BackupCommand)
