#!/bin/sh

while [ "$SQL_BACKUP" != "0" ]
do
  # Let's sleep until 23:59
  sleep $((24 * 3600 - 60 - (`date +%H` * 3600 + `date +%M` * 60 + `date +%S`)))

  # Backup timestamp
  export now=${1:-$(date +%Y%m%d_%H%M%S)}

  # Do a new backup
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /backup-db.sql >>/var/log/backup-db.log 2>&1

  # Delete backup files older than 30 days
  grep -B1 "BACKUP DATABASE successfully" /var/opt/mssql/log/errorlog | grep -q _$now.BAK &&
  find /etc/bitwarden/mssql/backups/ -mindepth 1 -type f -name '*.BAK' -mtime +32 -delete
done
