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

# Read the SA_PASSWORD value from a file for swarm environments.
# See https://github.com/Microsoft/mssql-docker/issues/326
if [ ! -z "$SA_PASSWORD" ] && [ ! -z "$SA_PASSWORD_FILE" ]
then
    echo "Provided both SA_PASSWORD and SA_PASSWORD_FILE environment variables. Please only use one."
    exit 1
fi
if [ ! -z "$SA_PASSWORD_FILE" ]
then
    # It should be exported, so it is available to the env command below.
    export SA_PASSWORD=$(cat $SA_PASSWORD_FILE)
fi

# Replace database name in backup-db.sql
if [ ! -z "$DATABASE" ]
then
  sed -i -e "/@DatabaseName /s/vault/$DATABASE/" backup-db.sql
  sed -i -e "/@DatabaseNameSafe /s/vault/${DATABASE// /-}/" backup-db.sql
fi

# The rest...

mkdir -p /etc/bitwarden/mssql/backups
chown -R $USERNAME:$GROUPNAME /etc/bitwarden
mkdir -p /var/opt/mssql/data
chown -R $USERNAME:$GROUPNAME /var/opt/mssql
chown $USERNAME:$GROUPNAME /backup-db.sh
chown $USERNAME:$GROUPNAME /backup-db.sql

# Launch a loop to backup database on a daily basis
if [ "$BACKUP_DB" != "0" ]
then
    gosu $USERNAME:$GROUPNAME /bin/sh -c "/backup-db.sh loop >/dev/null 2>&1 &"
fi

exec gosu $USERNAME:$GROUPNAME /opt/mssql/bin/sqlservr
