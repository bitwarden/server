#!/usr/bin/env bash

# DIR=$(cd -P "$(dirname "${BASH_SOURCE[0]}")" && pwd)

API_SECRETS=$(dotnet user-secrets list --project ../../src/Api/Api.csproj)

get_connection_string() {
  echo "$(echo "$API_SECRETS" | grep ^globalSettings:$1:connectionString | sed "s/globalSettings:$1:connectionString = //g")"
}

get_db() {
  # Takes
  DB_NAME=$(echo "$1" | perl -nle"print $& while m{$2=\w+}g" | sed "s/$2=//g")
  TEST_DB_NAME="${DB_NAME}_int_test"
  echo "$1" | sed "s/$DB_NAME/$TEST_DB_NAME/"
}

drop_and_migrate_ef() {
  # $1 should be connection string
  # $2 should be name of migrations project
  # $3 should be name of configuration section
  # $4 should be the index of where to save the
  # $5 *optional* Is the exact SupportedDatabaseProviders name of this database
  #     defaults to $3 if not specified
  if [ -z "$5" ]; then
    ENUM="$3"
  else
    ENUM="$5"
  fi

  # TODO: Filter out info messages and only show warning/error
  dotnet ef database drop --project ../../util/$2/ --startup-project ../../util/$2/ --force --prefix-output -- --GlobalSettings:$3:ConnectionString="$1" > /dev/null
  # TODO: Filter out info messages and only show warning/error
  dotnet ef database update --project ../../util/$2/ --startup-project ../../util/$2/ --connection "$1" --prefix-output -- --GlobalSettings:$3:ConnectionString="$1"

  dotnet user-secrets set "Databases:$4:Type" "$ENUM"
  dotnet user-secrets set "Databases:$4:ConnectionString" "$1" > /dev/null
  dotnet user-secrets set "Databases:$4:Enabled" true > /dev/null
}

# Server=localhost;Database=vault_dev;User Id=SA;Password=secure_password;TrustServerCertificate=true;
SQLSERVER_CONN_STR=$(get_connection_string "sqlServer")

# Host=localhost;Username=postgres;Password=secure_password;Database=vault_dev
POSTGRES_CONN_STR=$(get_connection_string "postgreSql")

# server=localhost;uid=root;pwd=secure_password;database=vault_dev
MYSQL_CONN_STR=$(get_connection_string "mySql")

# Data Source=/home/user/vault_dev.db
SQLITE_CONN_STR=$(get_connection_string "sqlite")

dotnet tool restore
dotnet user-secrets clear

INDEX=0

increment() {
  INDEX=$((INDEX + 1))
}

if [ ! -z "$SQLSERVER_CONN_STR" ]; then
  echo "You have a SQL Server Connection string."
  DB=$(get_db "$SQLSERVER_CONN_STR" "Database")
  dotnet run --project ../../util/MsSqlMigratorUtility -- "$DB"
  dotnet user-secrets set "Databases:$INDEX:Type" "SqlServer"
  dotnet user-secrets set "Databases:$INDEX:ConnectionString" "$DB" > /dev/null
  dotnet user-secrets set "Databases:$INDEX:Enabled" true > /dev/null
  increment
fi

echo

if [ ! -z "$POSTGRES_CONN_STR" ]; then
  echo "Making a test database for Postgres: $POSTGRES_CONN_STR"
  DB=$(get_db "$POSTGRES_CONN_STR" "Database")
  drop_and_migrate_ef "$DB" "PostgresMigrations" "PostgreSql" "$INDEX" "Postgres"
  increment
fi

echo

if [ ! -z "$MYSQL_CONN_STR" ]; then
  echo "Making a test database for MySql: $MYSQL_CONN_STR"
  DB=$(get_db "$MYSQL_CONN_STR" "database")
  # TODO: MySql needs that special variable, should we assist in that?
  drop_and_migrate_ef "$DB" "MySqlMigrations" "MySql" "$INDEX"
  increment
fi

echo

if [ ! -z "$SQLITE_CONN_STR" ]; then
  echo "Making a test database for Sqlite: $SQLITE_CONN_STR"
  # this makes the assumption it is a file which I think is likely for local dev
  FULL_DB_PATH=$(echo "$SQLITE_CONN_STR" | sed "s/Data Source=//g")
  FILE_NAME=$(basename "$FULL_DB_PATH")
  FILE_DIR=$(dirname "$FULL_DB_PATH")
  FILE_NAME_NO_EXT="${FILE_NAME%.*}"
  TEST_FILE_NAME="${FILE_NAME_NO_EXT}_int_test"
  TEST_DB_PATH="$FILE_DIR/$TEST_FILE_NAME.db"
  TEST_CONN_STR=$(echo "$SQLITE_CONN_STR" | sed "s~$FULL_DB_PATH~$TEST_DB_PATH~")
  drop_and_migrate_ef "$TEST_CONN_STR" "SqliteMigrations" "Sqlite" "$INDEX"
  increment
else
  echo "You don't have a SQLite Connection string, setting one up for you"
  TEST_DB_PATH="$(pwd)/test.db"
  drop_and_migrate_ef "Data Source=$TEST_DB_PATH" "SqliteMigrations" "Sqlite" "$INDEX"
  increment
fi

echo
