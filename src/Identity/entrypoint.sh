#!/bin/sh

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx
dotnet /app/Identity.dll
