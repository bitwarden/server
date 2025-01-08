#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/internal_dev
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

remove_comments() {
    # jq will not parse files with comments
    file="$1"

    if [[ -f "$file" ]]; then
        sed -e '/^\/\//d' -e 's@[[:blank:]]\{1,\}//.*@@' "$file" >"$file.tmp"
        mv "$file.tmp" "$file"
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

# Install Stripe CLI
install_stripe_cli() {
    echo "Installing Stripe CLI..."
    # Add Stripe CLI GPG key so that apt can verify the packages authenticity.
    curl -s https://packages.stripe.dev/api/security/keypair/stripe-cli-gpg/public | gpg --dearmor | sudo tee /usr/share/keyrings/stripe.gpg >/dev/null
    # Add Stripe CLI repository to apt sources
    echo "deb [signed-by=/usr/share/keyrings/stripe.gpg] https://packages.stripe.dev/stripe-cli-debian-local stable main" | sudo tee -a /etc/apt/sources.list.d/stripe.list >/dev/null
    sudo apt update
    sudo apt install -y stripe
}

one_time_setup() {
    # Load environment variables
    source_env_file "$DEV_DIR/.env"

    if [[ "$CONFIGURE_SECRETS_AND_CERTS" == "true" ]]; then
        echo "Running one-time setup script..."
        sleep 1
        
        if [[ -f "./dev/secrets.json" ]]; then
            remove_comments ./dev/secrets.json
        fi
        
        configure_secrets
        
        echo "Installing Az module. This will take ~a minute..."
        pwsh -Command "Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force"
        pwsh ./dev/setup_azurite.ps1

        dotnet tool install dotnet-certificate-tool -g >/dev/null

        if [[ -f "./dev/dev.pfx" ]]; then
            pushd ./dev >/dev/null || exit
            certificate-tool add --file ./dev.pfx --password "$LICENSING_CERT_PASSWORD"
            echo "Injecting dotnet secrets..."
            pwsh ./setup_secrets.ps1 || true
            popd >/dev/null || exit
        else
            echo "Warning: dev.pfx not found in ./dev directory"
        fi

        echo "Running migrations..."
        sleep 5 # wait for DB container to start
        dotnet run --project ./util/MsSqlMigratorUtility "$SQL_CONNECTION_STRING"
    fi

    if [[ "$INSTALL_STRIPE_CLI" == "true" ]]; then
        install_stripe_cli
    fi
}

# main
one_time_setup
