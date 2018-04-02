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
chown -R $USERNAME:$USERNAME /app
mkdir -p /bitwarden/env
mkdir -p /bitwarden/docker
mkdir -p /bitwarden/ssl
mkdir -p /bitwarden/letsencrypt
mkdir -p /bitwarden/identity
mkdir -p /bitwarden/nginx
chown -R $USERNAME:$USERNAME /bitwarden

exec gosu $USERNAME:$USERNAME "$@"
