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

chown -R bitwarden:bitwarden /app
mkdir -p /bitwarden/env
mkdir -p /bitwarden/docker
mkdir -p /bitwarden/ssl
mkdir -p /bitwarden/letsencrypt
mkdir -p /bitwarden/identity
mkdir -p /bitwarden/nginx
chown -R bitwarden:bitwarden /bitwarden

exec gosu bitwarden:bitwarden "$@"
