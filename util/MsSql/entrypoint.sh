#!/bin/bash

# Setup

GROUPNAME="bitwarden"
USERNAME="bitwarden"

LUID=${LOCAL_UID:-0}
LGID=${LOCAL_GID:-0}

# Step down from host root to well-known nobody/nogroup user

if [ $LUID -eq 0 ]
then
    LUID=65534
fi
if [ $LGID -eq 0 ]
then
    LGID=65534
fi

# Create user and group

groupadd -o -g $LGID $GROUPNAME >/dev/null 2>&1 ||
groupmod -o -g $LGID $GROUPNAME >/dev/null 2>&1
useradd -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1 ||
usermod -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1
mkhomedir_helper $USERNAME

# The rest...

# ref: https://stackoverflow.com/a/38850273
touch /var/log/cron.log /etc/crontab /etc/cron.*/*
chown $USERNAME:$GROUPNAME /var/log/cron.log
mkdir -p /etc/bitwarden/mssql/backups
chown -R $USERNAME:$GROUPNAME /etc/bitwarden
mkdir -p /var/opt/mssql/data
chown -R $USERNAME:$GROUPNAME /var/opt/mssql
chown $USERNAME:$GROUPNAME /backup-db.sh
chown $USERNAME:$GROUPNAME /backup-db.sql

# Sounds like gosu keeps env when switching, but of course cron does not
env > /etc/environment
cron

exec gosu $USERNAME:$GROUPNAME /opt/mssql/bin/sqlservr
