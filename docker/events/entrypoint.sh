#!/bin/bash

cp /etc/bitwarden/ca-certificates/*.crt /usr/local/share/ca-certificates/ >/dev/null 2>&1 \
    && update-ca-certificates

exec su-exec bitwarden:bitwarden dotnet /app/Events.dll