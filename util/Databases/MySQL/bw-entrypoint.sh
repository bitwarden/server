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

# Read the MYSQL_ROOT_PASSWORD value from a file for swarm environments.
# See https://github.com/Microsoft/mssql-docker/issues/326
if [ ! -z "$MYSQL_ROOT_PASSWORD" ] && [ ! -z "$MYSQL_ROOT_PASSWORD_FILE" ]
then
    echo "Provided both MYSQL_ROOT_PASSWORD and MYSQL_ROOT_PASSWORD_FILE environment variables. Please only use one."
    exit 1
fi
if [ ! -z "$MYSQL_ROOT_PASSWORD_FILE" ]
then
    # It should be exported, so it is available to the env command below.
    export MYSQL_ROOT_PASSWORD=$(cat $MYSQL_ROOT_PASSWORD_FILE)
fi

# Replace database name in backup-db.sql
if [ ! -z "$DATABASE" ]
then
  sed -i -e "/s/vault/$DATABASE/" backup-db.sh
  export MYSQL_DATABASE=$DATABASE
fi

# The rest...

mkdir -p /etc/bitwarden/database/backups
chown -R $USERNAME:$GROUPNAME /etc/bitwarden
chown $USERNAME:$GROUPNAME /backup-db.sh

# Launch a loop to backup database on a daily basis
if [ "$BACKUP_DB" != "0" ]
then
    gosu $USERNAME:$GROUPNAME /bin/sh -c "/backup-db.sh loop >/dev/null 2>&1 &"
fi

exec gosu $USERNAME:$GROUPNAME /entrypoint.sh
