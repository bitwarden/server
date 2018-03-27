#!/bin/sh

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

chown -R bitwarden:bitwarden /app
chown -R bitwarden:bitwarden /etc/iconserver

gosu bitwarden:bitwarden /etc/iconserver/iconserver &
gosu bitwarden:bitwarden dotnet /app/Icons.dll iconsSettings:bestIconBaseUrl=http://localhost:8080
