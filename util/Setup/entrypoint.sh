#!/bin/bash

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

chown -R bitwarden:bitwarden /app
mkdir -p /bitwarden/env
mkdir -p /bitwarden/docker
mkdir -p /bitwarden/ssl
mkdir -p /bitwarden/letsencrypt
mkdir -p /bitwarden/identity
mkdir -p /bitwarden/nginx
chown -R bitwarden:bitwarden /bitwarden

exec /usr/local/bin/gosu bitwarden:bitwarden "$@"
