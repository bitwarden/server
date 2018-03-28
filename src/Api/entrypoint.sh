#!/bin/bash

NOUSER=`id -u bitwarden > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}
if [ $NOUSER == 0 ] && [ `id -u bitwarden` != $LUID ]
then
    usermod -u $LUID bitwarden
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g bitwarden bitwarden
fi

touch /var/log/cron.log
chown bitwarden:bitwarden /var/log/cron.log
chown -R bitwarden:bitwarden /app
chown -R bitwarden:bitwarden /jobs
mkdir -p /etc/bitwarden/core
mkdir -p /etc/bitwarden/logs
chown -R bitwarden:bitwarden /etc/bitwarden

env >> /etc/environment
cron

gosu bitwarden:bitwarden dotnet /app/Api.dll
