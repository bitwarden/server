#!/bin/sh

env >> /etc/environment
cron

chown -R bitwarden:bitwarden /var/opt/mssql
gosu bitwarden:bitwarden /opt/mssql/bin/sqlservr
