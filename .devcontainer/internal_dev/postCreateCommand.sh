#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/internal_dev
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

remove_comments() {
    # jq will not parse files with comments
    file="$1"

    if [[ -f "$file" ]]; then
        sed -e '/^\/\//d' -e 's@[[:blank:]]\{1,\}//.*@@' "$file" >"$file.tmp"
        mv "$file.tmp" "$file"
    fi
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
    rm .secrets.json.tmp
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
        read -r -p \
            "Place the secrets.json and dev.pfx files from our shared Collection in the ./dev directory.
Press <Enter> to continue."
        remove_comments ./dev/secrets.json
        configure_other_vars
        echo "Installing Az module. This will take ~a minute..."
        pwsh -Command "Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force"
        pwsh ./dev/setup_azurite.ps1

        dotnet tool install dotnet-certificate-tool -g >/dev/null

        read -r -s -p "Paste the \"Licensing Certificate - Dev\" password: " CERT_PASSWORD
        echo
        pushd ./dev >/dev/null || exit
        certificate-tool add --file ./dev.pfx --password "$CERT_PASSWORD"
        echo "Injecting dotnet secrets..."
        pwsh ./setup_secrets.ps1 || true
        popd >/dev/null || exit

        echo "Running migrations..."
        sleep 5 # wait for DB container to start
        dotnet run --project ./util/MsSqlMigratorUtility "$SQL_CONNECTION_STRING"
    fi
    read -r -p "Would you like to install the Stripe CLI? [y/N] " stripe_response
    if [[ "$stripe_response" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        install_stripe_cli
    fi
}

# Install Stripe CLI
install_stripe_cli() {
    echo "Installing Stripe CLI..."
    # Add Stripe CLI GPG key so that apt can verify the packages authenticity.
    # If Stripe ever changes the key, we'll need to update this. Visit https://docs.stripe.com/stripe-cli?install-method=apt if so
    curl -s https://packages.stripe.dev/api/security/keypair/stripe-cli-gpg/public | gpg --dearmor | sudo tee /usr/share/keyrings/stripe.gpg >/dev/null
    # Add Stripe CLI repository to apt sources
    echo "deb [signed-by=/usr/share/keyrings/stripe.gpg] https://packages.stripe.dev/stripe-cli-debian-local stable main" | sudo tee -a /etc/apt/sources.list.d/stripe.list >/dev/null
    sudo apt update
    sudo apt install -y stripe
}

# main
one_time_setup
