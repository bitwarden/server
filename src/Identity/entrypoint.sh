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

if [ ! -d "/home/$USERNAME" ]
then
    mkhomedir_helper $USERNAME
fi

mkdir -p /etc/bitwarden/identity
mkdir -p /etc/bitwarden/core
mkdir -p /etc/bitwarden/logs
mkdir -p /etc/bitwarden/ca-certificates
chown -R $USERNAME:$USERNAME /etc/bitwarden

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx
chown -R $USERNAME:$USERNAME /app

cp /etc/bitwarden/ca-certificates/*.crt /usr/local/share/ca-certificates/ \
    && update-ca-certificates

gosu $USERNAME:$USERNAME dotnet /app/Identity.dll
