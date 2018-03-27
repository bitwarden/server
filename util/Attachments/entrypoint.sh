#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

chown -R bitwarden:bitwarden /bitwarden_server
mkdir -p /etc/bitwarden/core/attachments
chown -R bitwarden:bitwarden /etc/bitwarden

gosu bitwarden:bitwarden dotnet /bitwarden_server/Server.dll \
    /contentRoot=/etc/bitwarden/core/attachments /webRoot=. /serveUnknown=true
