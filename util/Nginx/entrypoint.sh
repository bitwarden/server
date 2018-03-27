#!/usr/bin/env bash

useradd -r -u ${LOCAL_UID:-999} -g bitwarden bitwarden

chown -R bitwarden:bitwarden /etc/bitwarden
cp /etc/bitwarden/nginx/default.conf /etc/nginx/conf.d/default.conf
mkdir -p /etc/letsencrypt
chown -R bitwarden:bitwarden /etc/letsencrypt
mkdir -p /etc/ssl
chown -R bitwarden:bitwarden /etc/ssl
touch /var/run/nginx.pid
chown bitwarden:bitwarden /var/run/nginx.pid
chown -R bitwarden:bitwarden /var/cache/nginx

gosu bitwarden:bitwarden nginx -g 'daemon off;'
