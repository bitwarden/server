#!/bin/sh

# Backup timestamp
export now=${1:-$(date +%Y%m%d_%H%M%S)}

# Do a new backup
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /backup-db.sql

# Delete backup files older than 30 days
grep -B1 "BACKUP DATABASE successfully" /var/opt/mssql/log/errorlog | grep -q _$now.BAK &&
find /etc/bitwarden/mssql/backups/ -mindepth 1 -type f -name '*.BAK' -mtime +32 -delete
