#!/bin/sh

cp /etc/core/identity.pfx /app/identity.pfx

dotnet /app/Identity.dll
