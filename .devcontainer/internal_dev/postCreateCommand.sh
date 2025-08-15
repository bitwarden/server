#!/usr/bin/env bash
export REPO_ROOT="$(git rev-parse --show-toplevel)"
export CONTAINER_CONFIG=/workspace/.devcontainer/internal_dev

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
    cp "$REPO_ROOT/dev/secrets.json" "$REPO_ROOT/dev/.secrets.json.tmp"
    # set DB_PASSWORD equal to .services.mssql.environment.MSSQL_SA_PASSWORD, accounting for quotes
    DB_PASSWORD="$(grep -oP 'MSSQL_SA_PASSWORD=["'"'"']?\K[^"'"'"'\s]+' $REPO_ROOT/dev/.env)"
    SQL_CONNECTION_STRING="Server=localhost;Database=vault_dev;User Id=SA;Password=$DB_PASSWORD;Encrypt=True;TrustServerCertificate=True"
    jq \
        ".globalSettings.sqlServer.connectionString = \"$SQL_CONNECTION_STRING\" |
        .globalSettings.postgreSql.connectionString = \"Host=localhost;Username=postgres;Password=$DB_PASSWORD;Database=vault_dev;Include Error Detail=true\" |
        .globalSettings.mySql.connectionString = \"server=localhost;uid=root;pwd=$DB_PASSWORD;database=vault_dev\"" \
        .secrets.json.tmp >secrets.json
    rm "$REPO_ROOT/dev/.secrets.json.tmp"
    popd >/dev/null || exit
}

one_time_setup() {
    if [[ ! -f "$REPO_ROOT/dev/dev.pfx" ]]; then
        # We do not have the cert file
        if [[ ! -z "${DEV_CERT_CONTENTS}" ]]; then
            # Make file for them
            echo "Making $REPO_ROOT/dev/dev.pfx file for you based on DEV_CERT_CONTENTS environment variable."
            # Assume content is base64 encoded
            echo "$DEV_CERT_CONTENTS" | base64 -d > "$REPO_ROOT/dev/dev.pfx"
        else
            if [[ $allow_interactive -eq 1 ]]; then
                read -r -p \
                    "Place the dev.pfx files from our shared Collection in the $REPO_ROOT/dev directory.
Press <Enter> to continue."
            fi
        fi
    fi

    if [[ -f "$REPO_ROOT/dev/dev.pfx" ]]; then
        dotnet tool install dotnet-certificate-tool -g >/dev/null
        cert_password="$(get_option "DEV_CERT_PASSWORD" "Paste the \"Licensing Certificate - Dev\" password: " "" 1)"
        certificate-tool add --file "$REPO_ROOT/dev/dev.pfx" --password "$cert_password"
    else
        echo "You don't have a $REPO_ROOT/dev/dev.pfx file setup." >/dev/stderr
    fi
    
    do_secrets_json_setup="$(get_option "SETUP_SECRETS_JSON" "Would you like us to setup your secrets.json file for you? [y/N] " "n")"
    if [[ "$do_secrets_json_setup" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        remove_comments "$REPO_ROOT/dev/secrets.json"
        configure_other_vars
        # setup_secrets needs to be ran from the dev folder
        pushd "$REPO_ROOT/dev" >/dev/null || exit
        echo "Injecting dotnet secrets..."
        pwsh "$REPO_ROOT/dev/setup_secrets.ps1" || true
        popd >/dev/null || exit
    fi

    do_azurite_setup="$(get_option "SETUP_AZURITE" "Would you like us to setup your azurite environment? [y/N] " "n")"
    if [[ "$do_azurite_setup" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        echo "Installing Az module. This will take ~a minute..."
        pwsh -Command "Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force"
        pwsh "$REPO_ROOT/dev/setup_azurite.ps1"
    fi

    run_mssql_migrations="$(get_option "RUN_MSSQL_MIGRATIONS" "Would you like us to run MSSQL Migrations for you? [y/N] " "n")"
    if [[ "$do_azurite_setup" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        echo "Running migrations..."
        sleep 5 # wait for DB container to start
        dotnet run --project "$REPO_ROOT/util/MsSqlMigratorUtility" "$SQL_CONNECTION_STRING"
    fi

    stripe_response="$(get_option "INSTALL_STRIPE_CLI" "Would you like to install the Stripe CLI? [y/N] " "n")"
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

one_time_setup
