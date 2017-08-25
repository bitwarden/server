#!/bin/sh

dotnet /bitwarden_server/Server.dll /contentRoot=/etc/bitwarden/core/attachments /webRoot=. /serveUnknown=true
