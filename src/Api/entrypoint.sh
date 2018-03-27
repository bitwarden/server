#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

touch /var/log/cron.log
chown bitwarden:bitwarden /var/log/cron.log
chown -R bitwarden:bitwarden /app
chown -R bitwarden:bitwarden /jobs
mkdir -p /etc/bitwarden/core
chown -R bitwarden:bitwarden /etc/bitwarden

env >> /etc/environment
cron

gosu bitwarden:bitwarden dotnet /app/Api.dll
