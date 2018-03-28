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
chown -R bitwarden:bitwarden /etc/iconserver

gosu bitwarden:bitwarden /etc/iconserver/iconserver &
gosu bitwarden:bitwarden dotnet /app/Icons.dll iconsSettings:bestIconBaseUrl=http://localhost:8080
