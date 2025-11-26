#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/community_dev
git config --global --add safe.directory /workspace

if [[ -z "${CODESPACES}" ]]; then
    allow_interactive=1
else
    echo "Doing non-interactive setup"
    allow_interactive=0
fi

get_option() {
    # Helper function for reading the value of an environment variable
    # primarily but then falling back to an interactive question if allowed
    # and lastly falling back to a default value input when either other
    # option is available.
    name_of_var="$1"
    question_text="$2"
    default_value="$3"
    is_secret="$4"

    if [[ -n "${!name_of_var}" ]]; then
        # If the env variable they gave us has a value, then use that value
        echo "${!name_of_var}"
    elif [[ "$allow_interactive" == 1 ]]; then
        # If we can be interactive, then use the text they gave us to request input
        if [[ "$is_secret" == 1 ]]; then
            read -r -s -p "$question_text" response
            echo "$response"
        else
            read -r -p "$question_text" response
            echo "$response"
        fi
    else
        # If no environment variable and not interactive, then just give back default value
        echo "$default_value"
    fi
}

get_installation_id_and_key() {
    pushd ./dev >/dev/null || exit
    echo "Please enter your installation id and key from https://bitwarden.com/host:"
    INSTALLATION_ID="$(get_option "INSTALLATION_ID" "Installation id: " "00000000-0000-0000-0000-000000000001")"
    INSTALLATION_KEY="$(get_option "INSTALLATION_KEY", "Installation key: " "" 1)"
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
    do_secrets_json_setup="$(get_option "SETUP_SECRETS_JSON" "Would you like to configure your secrets and certificates for the first time?
WARNING: This will overwrite any existing secrets.json and certificate files.
Proceed? [y/N] " "n")"
    if [[ "$do_secrets_json_setup" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
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

one_time_setup
