#!/bin/bash

USERNAME="bitwarden"
NOUSER=`id -u $USERNAME > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}

# Step down from host root
if [ $LUID == 0 ]
then
    LUID=999
fi

if [ $NOUSER == 0 ] && [ `id -u $USERNAME` != $LUID ]
then
    usermod -u $LUID $USERNAME
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g $USERNAME $USERNAME
fi

mkdir -p /home/$USERNAME
chown -R $USERNAME:$USERNAME /home/$USERNAME
touch /var/log/cron.log
chown $USERNAME:$USERNAME /var/log/cron.log
mkdir -p /etc/bitwarden/mssql/backups
chown -R $USERNAME:$USERNAME /etc/bitwarden
mkdir -p /var/opt/mssql/data
chown -R $USERNAME:$USERNAME /var/opt/mssql
chown $USERNAME:$USERNAME /backup-db.sh
chown $USERNAME:$USERNAME /backup-db.sql

env >> /etc/environment
cron

gosu $USERNAME:$USERNAME /opt/mssql/bin/sqlservr
