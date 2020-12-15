#!/bin/sh
BACKUP_INTERVAL=${BACKUP_INTERVAL:-next day}
BACKUP_INTERVAL_FORMAT=${BACKUP_INTERVAL_FORMAT:-%Y-%m-%d 00:00:00}

while true
do
  # Sleep until next day
  if [ "$1" = "loop" ]; then
    interval_start=`date "+${BACKUP_INTERVAL_FORMAT} %z" -d "${BACKUP_INTERVAL}"`
    sleep $((`date +%_s -d "${interval_start}"` - `date +%_s`))
  fi

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
