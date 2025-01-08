#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/community_dev
git config --global --add safe.directory /workspace

# Function to sanitize and source the .env file
source_env_file() {
    local env_file="$1"
    if [[ -f "$env_file" ]]; then
        # Read the file line by line, removing carriage returns
        while IFS= read -r line || [ -n "$line" ]; do
            # Skip empty lines and comments
            [[ -z "$line" || "$line" == \#* ]] && continue
            
            # Remove carriage returns and trim whitespace
            line=$(echo "$line" | tr -d '\r' | xargs)
            
            # Split into variable name and value
            if [[ "$line" =~ ^([^=]+)=(.*)$ ]]; then
                local name="${BASH_REMATCH[1]}"
                local value="${BASH_REMATCH[2]}"
                
                # Remove surrounding quotes if they exist
                value="${value#\"}"
                value="${value%\"}"
                value="${value#\'}"
                value="${value%\'}"
                
                # Export with proper quoting
                export "$name=$value"
            fi
        done < "$env_file"
    fi
}

configure_secrets() {
    pushd ./dev >/dev/null || exit
    # Create secrets.json from example if it doesn't exist
    if [[ ! -f "secrets.json" ]]; then
        cp secrets.json.example secrets.json
    fi
    
    # Configure secrets
    cp secrets.json .secrets.json.tmp
    SQL_CONNECTION_STRING="Server=localhost;Database=vault_dev;User Id=SA;Password=$MSSQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
    jq \
        ".globalSettings.sqlServer.connectionString = \"$SQL_CONNECTION_STRING\" |
        .globalSettings.postgreSql.connectionString = \"Host=localhost;Username=postgres;Password=$POSTGRES_PASSWORD;Database=vault_dev;Include Error Detail=true\" |
        .globalSettings.mySql.connectionString = \"server=localhost;uid=root;pwd=$MYSQL_ROOT_PASSWORD;database=vault_dev\"" \
        .secrets.json.tmp >secrets.json
    rm -f .secrets.json.tmp
    popd >/dev/null || exit
}

one_time_setup() {
    # Load environment variables
    source_env_file "$DEV_DIR/.env"

    if [[ "$CONFIGURE_SECRETS_AND_CERTS" == "true" ]]; then
        echo "Running one-time setup script..."
        sleep 1
        configure_secrets
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
