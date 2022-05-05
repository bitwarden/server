#!/bin/sh

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx

exec dotnet /app/Sso.dll
