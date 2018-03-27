#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

mkdir -p /etc/bitwarden/identity
mkdir -p /etc/bitwarden/core
chown -R bitwarden:bitwarden /etc/bitwarden

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx
chown -R bitwarden:bitwarden /app

gosu bitwarden:bitwarden dotnet /app/Identity.dll
