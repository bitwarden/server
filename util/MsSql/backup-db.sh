#!/bin/sh

# Delete backup files older than 30 days
find /etc/bitwarden/mssql/backups/ -type f -name '*.BAK' -mindepth 1 -mtime +30 -delete

# Do a new backup
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /backup-db.sql
