#!/bin/sh

env >> /etc/environment
cron

gosu bitwarden:bitwarden dotnet /app/Api.dll
