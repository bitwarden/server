#!/bin/sh

NOUSER=`id -u bitwarden > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}
if [[ $NOUSER == 0 && `id -u bitwarden` != $LUID ]]
then
    usermod -u $LUID bitwarden
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g bitwarden bitwarden
fi

touch /var/log/cron.log
chown bitwarden:bitwarden /var/log/cron.log
mkdir -p /etc/bitwarden/mssql/backups
chown -R bitwarden:bitwarden /etc/bitwarden
mkdir -p /var/opt/mssql/data
chown -R bitwarden:bitwarden /var/opt/mssql
chown bitwarden:bitwarden /backup-db.sh
chown bitwarden:bitwarden /backup-db.sql

env >> /etc/environment
cron

gosu bitwarden:bitwarden /opt/mssql/bin/sqlservr
