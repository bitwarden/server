#!/bin/sh

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx
cp /etc/bitwarden/ca-certificates/*.crt /usr/local/share/ca-certificates/ >/dev/null 2>&1 \
    && update-ca-certificates

exec dotnet /app/Sso.dll