#!/bin/bash
#
# !!! UPDATED 2024 for MsSqlMigratorUtility !!!
#
# There seems to be [a bug with docker-compose](https://github.com/docker/compose/issues/4076#issuecomment-324932294)
# where it takes ~40ms to connect to the terminal output of the container, so stuff logged to the terminal in this time is lost.
# The best workaround seems to be adding tiny delay like so:
sleep 0.1;

SERVER='mssql'
DATABASE="vault_dev"
USER="SA"
PASSWD=$MSSQL_PASSWORD

while getopts "s" arg; do
  case $arg in
    s)
      echo "Running for self-host environment"
      DATABASE="vault_dev_self_host"
      ;;
  esac
done

QUERY="IF OBJECT_ID('[$DATABASE].[dbo].[Migration]') IS NULL AND OBJECT_ID('[migrations_$DATABASE].[dbo].[migrations]') IS NOT NULL
BEGIN
    -- Create [database].dbo.Migration with the schema expected by MsSqlMigratorUtility
    SET ANSI_NULLS ON;
    SET QUOTED_IDENTIFIER ON;

    CREATE TABLE [$DATABASE].[dbo].[Migration](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [ScriptName] [nvarchar](255) NOT NULL,
        [Applied] [datetime] NOT NULL
    ) ON [PRIMARY];

    ALTER TABLE [$DATABASE].[dbo].[Migration] ADD  CONSTRAINT [PK_Migration_Id] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];

    -- Copy across old data
    INSERT INTO [$DATABASE].[dbo].[Migration] (ScriptName, Applied)
    SELECT CONCAT('Bit.Migrator.DbScripts.', [Filename]), CreationDate
    FROM [migrations_$DATABASE].[dbo].[migrations];
END
"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"
