#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/community_dev
git config --global --add safe.directory /workspace

get_installation_id_and_key() {
    pushd ./dev >/dev/null || exit
    echo "Please enter your installation id and key from https://bitwarden.com/host:"
    read -r -p "Installation id: " INSTALLATION_ID
    read -r -p "Installation key: " INSTALLATION_KEY
    jq ".globalSettings.installation.id = \"$INSTALLATION_ID\" |
        .globalSettings.installation.key = \"$INSTALLATION_KEY\"" \
        secrets.json.example >secrets.json # create/overwrite secrets.json
    popd >/dev/null || exit
}

configure_other_vars() {
    pushd ./dev >/dev/null || exit
    cp secrets.json .secrets.json.tmp
    # set DB_PASSWORD equal to .services.mssql.environment.MSSQL_SA_PASSWORD, accounting for quotes
    DB_PASSWORD="$(grep -oP 'MSSQL_SA_PASSWORD=["'"'"']?\K[^"'"'"'\s]+' $DEV_DIR/.env)"
    SQL_CONNECTION_STRING="Server=localhost;Database=vault_dev;User Id=SA;Password=$DB_PASSWORD;Encrypt=True;TrustServerCertificate=True"
    jq \
        ".globalSettings.sqlServer.connectionString = \"$SQL_CONNECTION_STRING\" |
        .globalSettings.postgreSql.connectionString = \"Host=localhost;Username=postgres;Password=$DB_PASSWORD;Database=vault_dev;Include Error Detail=true\" |
        .globalSettings.mySql.connectionString = \"server=localhost;uid=root;pwd=$DB_PASSWORD;database=vault_dev\"" \
        .secrets.json.tmp >secrets.json
    rm -f .secrets.json.tmp
    popd >/dev/null || exit
}

one_time_setup() {
    read -r -p \
        "Would you like to configure your secrets and certificates for the first time?
WARNING: This will overwrite any existing secrets.json and certificate files.
Proceed? [y/N] " response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        echo "Running one-time setup script..."
        sleep 1
        get_installation_id_and_key
        configure_other_vars
        pushd ./dev >/dev/null || exit
        pwsh ./setup_secrets.ps1 || true
        popd >/dev/null || exit

        echo "Running migrations..."
        sleep 5 # wait for DB container to start
        dotnet run --project ./util/MsSqlMigratorUtility "$SQL_CONNECTION_STRING"

    fi
}

# main
one_time_setup
