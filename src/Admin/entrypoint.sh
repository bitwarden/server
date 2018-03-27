#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

chown -R bitwarden:bitwarden /app
mkdir -p /etc/bitwarden/core
chown -R bitwarden:bitwarden /etc/bitwarden

gosu bitwarden:bitwarden dotnet /app/Admin.dll
