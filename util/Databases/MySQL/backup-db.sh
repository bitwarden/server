#!/bin/sh
BACKUP_INTERVAL=${BACKUP_INTERVAL:-next day}
BACKUP_INTERVAL_FORMAT=${BACKUP_INTERVAL_FORMAT:-%Y-%m-%d 00:00:00}

while true
do
  # Sleep until next day
  if [ "$1" = "loop" ]; then
    interval_start=$(date "+${BACKUP_INTERVAL_FORMAT} %z" -d "${BACKUP_INTERVAL}")
    sleep $(($(date +%_s -d "${interval_start}") - $(date +%_s)))
  fi

  # Backup timestamp
  now=$(date +%Y%m%d_%H%M%S)
  export now

  # Do a new backup
  /usr/bin/mysqldump -u root -p "${MYSQL_ROOT_PASSWORD}" "vault" > /etc/bitwarden/database/backups/"$now".sql

  # Delete backup files older than 30 days
  find /etc/bitwarden/database/backups/ -mindepth 1 -type f -name '*.sql' -mtime +32 -delete

  # Break if called manually (without loop option)
  [ "$1" != "loop" ] && break
done
