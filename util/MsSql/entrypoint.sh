#!/bin/sh

/opt/mssql/bin/sqlservr
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -i /setup.sql
