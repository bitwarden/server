#!/bin/sh

env >> /etc/environment
cron
dotnet /app/Api.dll
