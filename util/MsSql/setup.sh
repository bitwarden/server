#!/bin/sh

sleep 60s
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /setup.sql
