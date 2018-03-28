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

chown -R bitwarden:bitwarden /bitwarden_server
mkdir -p /etc/bitwarden/core/attachments
chown -R bitwarden:bitwarden /etc/bitwarden

gosu bitwarden:bitwarden dotnet /bitwarden_server/Server.dll \
    /contentRoot=/etc/bitwarden/core/attachments /webRoot=. /serveUnknown=true
