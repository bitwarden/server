#!/bin/sh
BACKUP_INTERVAL=${BACKUP_INTERVAL:-next day}
BACKUP_INTERVAL_FORMAT=${BACKUP_INTERVAL_FORMAT:-%Y-%m-%d 00:00:00}
export BACKUP_DB_DIR=${BACKUP_DB_DIR:-'/etc/bitwarden/mssql/backups/'}
BACKUP_DB_KEEP_MTIME=${BACKUP_DB_KEEP_MTIME:-'+32'}

while true
do
  # Sleep until next day
  if [ "$1" = "loop" ]; then
    interval_start=`date "+${BACKUP_INTERVAL_FORMAT} %z" -d "${BACKUP_INTERVAL}"`
    sleep $((`date +%_s -d "${interval_start}"` - `date +%_s`))
  fi

  # Backup timestamp
  export now=$(date +%Y%m%d_%H%M%S)
  BACKUP_DB_FILENAME=${BACKUP_DB_FILENAME:-"${now}"}
  export BACKUP_DB_FILENAME="${BACKUP_DB_FILENAME}${BACKUP_DB_FILENAME_SUFFIX}"

  # Do a new backup
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /backup-db.sql

  # Delete backup files older than 30 days
  grep -B1 "BACKUP DATABASE successfully" /var/opt/mssql/log/errorlog | grep -q _${BACKUP_DB_FILENAME}.BAK &&
  find $BACKUP_DB_DIR -mindepth 1 -type f -name '*.BAK' -mtime $BACKUP_DB_KEEP_MTIME -delete

  # Common variables used in the next two if blocks.
  DATABASE=${DATABASE:-'vault'}
  BACKUP_DB_LATEST_FILENAME="${DATABASE}_FULL_${BACKUP_DB_FILENAME}.BAK"

  # Make a copy (overwrite) with a consistent filename.
  # Helps when taking snapshots with deduplication algorithms e.g. restic
  if [ "true" = "$BACKUP_DB_COPYOFLATEST" ]; then    
    BACKUP_DB_COPYOFLATEST_FILENAME=${BACKUP_DB_COPYOFLATEST_FILENAME:-"${DATABASE}_FULL_LATESTCOPY.BAK"}
    cp -f "${BACKUP_DB_DIR}${BACKUP_DB_LATEST_FILENAME}" "${BACKUP_DB_DIR}${BACKUP_DB_COPYOFLATEST_FILENAME}"
  fi

  # Make a symlink
  # Helps in all other circumstances where an actual copy of the file isn't necessary.
  if [ "true" = "$BACKUP_DB_LNFORLATEST" ]; then
    BACKUP_DB_LNFORLATEST_FILENAME=${BACKUP_DB_LNFORLATEST_FILENAME:-"${DATABASE}_FULL_LATESTLN.BAK"}
    ln -sf "${BACKUP_DB_LATEST_FILENAME}" "${BACKUP_DB_DIR}${BACKUP_DB_LNFORLATEST_FILENAME}"
  fi

  # Break if called manually (without loop option)
  [ "$1" != "loop" ] && break
done
