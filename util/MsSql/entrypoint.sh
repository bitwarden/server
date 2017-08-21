#!/bin/sh

env >> /etc/environment
cron
/opt/mssql/bin/sqlservr
