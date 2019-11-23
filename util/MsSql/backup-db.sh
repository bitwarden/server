#!/bin/sh

while true
do
  # Sleep until next day
  [ "$1" == "loop" ] && sleep $((24 * 3600 - (`date +%H` * 3600 + `date +%M` * 60 + `date +%S`)))

  # Backup timestamp
  export now=$(date +%Y%m%d_%H%M%S)

  # Do a new backup
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /backup-db.sql

  # Delete backup files older than 30 days
  grep -B1 "BACKUP DATABASE successfully" /var/opt/mssql/log/errorlog | grep -q _$now.BAK &&
  find /etc/bitwarden/mssql/backups/ -mindepth 1 -type f -name '*.BAK' -mtime +32 -delete

  # Break if called manually (without loop option)
  [ "$1" != "loop" ] && break
done
