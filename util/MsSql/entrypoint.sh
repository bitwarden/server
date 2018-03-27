#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

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
